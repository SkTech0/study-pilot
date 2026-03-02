# StudyPilot — Engineering Documentation

Production-level technical documentation for the StudyPilot system. This document describes the actual implementation, architecture, and operational characteristics.

---

# 1. Project Overview

## What StudyPilot Solves

StudyPilot is a learning-assistant system that turns user-uploaded PDF documents into structured concepts and interactive quizzes. Users upload a PDF; the system extracts concepts via an LLM, persists them, and enables on-demand quiz generation with per-concept mastery tracking.

## Core Idea

- **Asynchronous AI pipeline:** The API never blocks on AI. Document metadata is stored immediately; processing runs in a background worker. Quiz questions are generated lazily (on first view) to avoid long waits and to reduce token usage.
- **Multi-LLM resilience:** The Python AI service uses a configurable provider chain (e.g. Gemini → DeepSeek → OpenRouter → OpenAI). On 429 or failure, the next provider is tried with minimal delay so users see seamless fallback.
- **Mastery model:** User answers update per-concept progress (MasteryScore 0–100, attempts, accuracy). Weak topics are derived from low mastery for dashboard display.

## Product Architecture Philosophy

- **Clean Architecture (strict):** Domain has no external dependencies. Application defines use cases and abstractions (persistence, AI, background jobs, observability). Infrastructure and API implement them. Dependencies point inward.
- **CQRS with MediatR:** Commands and queries are explicit; handlers encapsulate business logic. Validation and logging are pipeline behaviors.
- **API returns quickly:** Upload returns document ID and status; processing and quiz generation happen asynchronously. Frontend polls document status and loads questions on demand.

## Why Async AI Pipeline Was Chosen

- LLM calls can take tens of seconds to minutes; holding HTTP requests would cause timeouts and poor UX.
- Decoupling via a queue allows retries, correlation, and future scaling (multiple workers, different backends) without changing the API contract.
- Lazy question generation keeps quiz start instant and spreads AI load over user interaction instead of one large batch.

---

# 2. High-Level Architecture

```
┌─────────────┐     ┌─────────────┐     ┌──────────────────┐     ┌─────────────────┐
│  Angular    │────▶│  ASP.NET    │────▶│  Application     │────▶│  Infrastructure │
│  (4200)     │     │  Core API   │     │  (CQRS/MediatR)   │     │  (Persistence,   │
│             │     │  (5024)     │     │                  │     │   AI client,     │
└─────────────┘     └─────────────┘     └────────┬─────────┘     │   Queue, Auth)  │
       │                     │                    │              └────────┬────────┘
       │                     │                    │                       │
       │                     │                    ▼                       ▼
       │                     │            ┌───────────────┐        ┌──────────────┐
       │                     │            │   Domain      │        │  Background   │
       │                     │            │   (Entities,  │        │  Job Worker  │
       │                     │            │   ValueObjs)  │        └──────┬───────┘
       │                     │            └───────────────┘               │
       │                     │                                           │
       │                     │                     ┌──────────────────────┘
       │                     │                     │
       │                     ▼                     ▼
       │              ┌─────────────┐     ┌─────────────┐     ┌─────────────┐
       └──────────────▶│  PostgreSQL │◀────│  FastAPI    │◀────│  AI Python  │
                       │  (EF Core)  │     │  AI Client  │     │  (8000)     │
                       └─────────────┘     └─────────────┘     └─────────────┘
```

**Layer responsibilities**

| Layer | Responsibility |
|-------|----------------|
| **Frontend (Angular)** | Auth (JWT + refresh), document list/upload, document polling until Processing→Completed/Failed, quiz start/question fetch/submit, weak-topics dashboard, global error handling and toasts. |
| **API (ASP.NET Core)** | Controllers, request validation, file storage, rate limiting, JWT auth, correlation ID, global exception mapping to HTTP and `ApiResponse&lt;T&gt;`. No business logic. |
| **Application** | Command/query handlers, validation (FluentValidation), logging behavior, Result&lt;T&gt; and error codes. Depends only on Domain and abstractions (persistence, AI, jobs, observability, usage guard). |
| **Domain** | Entities (User, Document, Concept, Quiz, Question, UserAnswer, UserConceptProgress), value objects (Email, MasteryScore), enums (ProcessingStatus, QuestionGenerationStatus). No infrastructure. |
| **Infrastructure** | EF Core DbContext and repositories, UnitOfWork, HTTP client for Python AI service, in-memory background job queue, background worker, file storage (local), JWT generation, password hashing, usage guards, correlation ID accessor, Serilog, worker heartbeat. |
| **Worker** | Single BackgroundService that dequeues jobs (document processing). Each job runs in a scoped scope; calls file reader → AI client (extract-concepts) → concept persistence → status update. |
| **AI Service (FastAPI)** | Two endpoints: POST /extract-concepts, POST /generate-quiz. Uses provider abstraction (Gemini, DeepSeek, OpenRouter, OpenAI) with FallbackAdapter; retries and failover on 429/5xx. |

**Dependency direction:** API and Infrastructure reference Application and Domain. Application references only Domain and its own abstractions. Domain has no project references.

---

# 3. System Flow (Step-by-Step)

## Document Upload Flow

1. **Controller** (`DocumentsController.Upload`): Validates file (non-null, size ≤10MB), path traversal and double-extension checks (`UploadSecurity`), PDF content-type and magic-byte validation. Sanitizes filename, saves via `IFileStorage.SaveAsync` (path: `uploads/{userId}/{guid}_{filename}`), then sends `UploadDocumentCommand(userId, fileName, storagePath)`.
2. **Command handler** (`UploadDocumentCommandHandler`): Checks `IUsageGuardService.CanUploadDocumentAsync` (per-user per-day limit). Creates `Document` in state Pending, adds via `IDocumentRepository`, `IUnitOfWork.SaveChangesAsync`. Gets correlation ID from `ICorrelationIdAccessor`, enqueues `IDocumentProcessingJobFactory.CreateProcessDocumentJob(documentId, correlationId)` on `IBackgroundJobQueue`. Returns `UploadDocumentResult(documentId)` immediately. **API does not wait for AI.**
3. **Queue:** `InMemoryBackgroundJobQueue` wraps a single unbounded `Channel<Func<CancellationToken, Task>>`. `Enqueue` writes the delegate; no persistence.
4. **Worker:** `BackgroundJobWorker` loops: `DequeueAsync` (blocks until a job exists), invokes the delegate, catches exceptions (logs, increments failure metric), does not re-enqueue. Correlation ID is set on the scope before running the job.

## Document Processing Flow (Worker)

1. **Job delegate** (from `DocumentProcessingJobFactory.CreateProcessDocumentJob`): Creates a new DI scope, resolves repositories, `IStudyPilotAIClient`, `IFileContentReader`, options. Sets correlation ID on the accessor if provided.
2. **Claim:** `IDocumentRepository.TryClaimForProcessingAsync(documentId)` runs an EF Core `ExecuteUpdateAsync` that sets `ProcessingStatus = Processing` only where `Id = documentId` and `ProcessingStatus = Pending`. If no row is updated, returns null and the job exits (idempotent claim).
3. **Read:** `IFileContentReader.ReadAllTextAsync(storagePath)` uses PdfPig to extract text from the PDF; path is validated to be under `StorageOptions.UploadsBasePath` to prevent path traversal.
4. **Length check:** If text length &gt; `AIServiceOptions.MaxTextLength` (default 50_000), job fails with status Failed and sanitized reason.
5. **AI call:** `IStudyPilotAIClient.ExtractConceptsAsync(documentId, text)` POSTs to the Python service `/extract-concepts` with JSON `{ documentId, text }`. Python returns `{ concepts: [{ name, description }] }`.
6. **Persistence:** Existing concepts for the document are deleted; new `Concept` entities are added; `document.SetProcessingStatus(Completed)` and update. On any exception, status is set to Failed with a truncated message (max 500 chars). On cancellation, status is set to Failed with "Processing was cancelled."

**State transitions:** Pending → Processing (on claim) → Completed or Failed.

## Quiz Generation Flow

1. **Start quiz:** `StartQuizCommand(documentId, userId)`. Handler checks usage guard (quiz/hour limit), loads document and concepts. Creates `Quiz(documentId, userId, totalQuestionCount)` with `totalQuestionCount = Min(5, Max(1, conceptCount))`. No questions are created yet; quiz is persisted.
2. **Get question (lazy):** `GetQuizQuestionQuery(quizId, questionIndex)`. Handler loads quiz and tries to get existing question for that index. If none, creates a placeholder `Question` with `Status = Generating` and tries `IQuizRepository.TryAddQuestionAsync` (handles unique constraint on quiz+index). If inserted, calls `IQuestionGenerationDispatcher.DispatchAsync(quizId, questionIndex)` and reloads the question. If status is still Generating, returns that (frontend can poll or retry). If Failed, returns with error message. If Ready, returns text and options. As an optimization, the handler fires a background task to dispatch the next index so the next question may already be generating.
3. **Dispatch (question generation):** `QuestionGenerationDispatcher.DispatchAsync` loads the question (must be in Generating), quiz, and concepts. Maps concept at `questionIndex` to `ConceptInfo`, then calls `IAIService.GenerateQuestionAsync(documentId, userId, conceptInfo)` (which uses the HTTP client to POST `/generate-quiz` with a single concept and count 1). On success, `question.MarkReady(...)` and link question to concept in `QuestionConceptLinks`. Up to 3 retries with exponential backoff (`QuestionGenerationRetryBaseDelayMs`); on final failure, `MarkFailed(message)`.
4. **Submit quiz:** `SubmitQuizCommand(quizId, userId, answers)`. Handler loads quiz with questions. For each question, resolves correct answer (option text or index A–D / 1–4), compares with submitted answer (text or option index), creates `UserAnswer`, finds or creates `UserConceptProgress` for the linked concept, and calls `RecordCorrectAnswer` or `RecordWrongAnswer` (MasteryScore ±10/−5, clamped 0–100). Weak-topics cache key for the user is invalidated. Returns score and per-question results.

## Mastery Tracking Flow

- **UserConceptProgress:** One row per (UserId, ConceptId). Holds MasteryScore (0–100), Attempts, internal correct count, LastReviewedUtc. `RecordCorrectAnswer` increases score by 10 and attempt/correct count; `RecordWrongAnswer` decreases by 5 and attempt count. Value object `MasteryScore` clamps to [0, 100].
- **Weak topics:** `GetWeakConceptsQuery(userId)` returns concepts where the user has progress below a threshold (and optionally least recently reviewed). Result is cached (e.g. invalidated on quiz submit) for the weak-topics dashboard.

## Authentication Flow

1. **Register:** `RegisterCommand` → validate email/password, check user does not exist, hash password, create `User`, persist, generate access + refresh tokens, store refresh token, return tokens and expiry.
2. **Login:** `LoginCommand` → find user by email, verify password hash, generate access + refresh tokens, store refresh token, return tokens and expiry.
3. **Refresh:** `RefreshTokenCommand` → validate refresh token (exists, not revoked, not expired), issue new access + refresh, revoke old refresh, return new tokens.
4. **Logout:** `LogoutCommand` → revoke refresh token by value.
5. **API:** JWT Bearer on protected endpoints; claims include user id (sub). Frontend stores access token in localStorage, refresh in sessionStorage; on 401, error interceptor attempts refresh once, then redirects to login.

---

# 4. Backend Architecture Deep Dive

## Domain Layer

- **Entities:** Encapsulate state and invariants. Document, Concept, Quiz, Question, UserAnswer, UserConceptProgress, User. BaseEntity provides Id, CreatedAtUtc, UpdatedAtUtc; `Touch()` updates UpdatedAtUtc on change.
- **Value objects:** Email (validation), MasteryScore (0–100, Increase/Decrease).
- **Enums:** ProcessingStatus (Pending, Processing, Completed, Failed), QuestionGenerationStatus (Generating, Ready, Failed).
- No dependencies on persistence or external services; used by Application and Infrastructure.

## Application CQRS Design

- **Commands:** UploadDocument, StartQuiz, SubmitQuiz, Register, Login, Logout, RefreshToken. Return `Result<T>` with success value or list of `AppError` (code, message, field, severity).
- **Queries:** GetDocuments, GetQuizQuestion, GetWeakConcepts. Same Result pattern.
- Handlers use abstractions: `I*Repository`, `IUnitOfWork`, `IBackgroundJobQueue`, `IDocumentProcessingJobFactory`, `IAIService`, `IQuestionGenerationDispatcher`, `IUsageGuardService`, `ICorrelationIdAccessor`, etc. No direct EF or HTTP.

## MediatR Pipeline Behaviors

- **ValidationBehavior:** Runs after validators are registered; runs all `IValidator<TRequest>`. On any failure, throws `ValidationException` (mapped to 400 by middleware).
- **LoggingBehavior:** Logs request type and correlation ID before and after the handler. Uses `IRequestLogger` and `ICorrelationIdAccessor`.

Order: Validation runs first, then Logging, then handler.

## Repository Pattern and UnitOfWork

- Repositories are scoped; one per aggregate or query need (Document, Concept, Quiz, User, UserAnswer, UserConceptProgress, RefreshToken, QuestionConceptLink). They return domain entities; DocumentRepository exposes `TryClaimForProcessingAsync` for the worker’s single-claim semantics.
- **UnitOfWork:** Wraps `StudyPilotDbContext`. `SaveChangesAsync` is the single commit point. Handlers call repositories then `_unitOfWork.SaveChangesAsync()` so multiple changes in one request are atomic within the DB.

## Background Job Design

- **Queue:** In-memory only (`Channel`). No durability; process restart drops pending jobs.
- **Factory:** `DocumentProcessingJobFactory` creates a closure that, when run, creates a new scope and executes the document-processing steps. Correlation ID is passed so logs can be traced.
- **Worker:** One `BackgroundService`; single-threaded dequeue and execute. No competing consumers; no backpressure beyond channel capacity.
- **Idempotency:** Claim uses `ExecuteUpdateAsync` on (Id, Pending) → Processing. Only one worker will succeed per document; duplicate jobs leave status unchanged and exit.

## Distributed Safety and CorrelationId

- **CorrelationId:** Set in API middleware from header or new GUID; stored in `ICorrelationIdAccessor` (async local / request-scoped). Added to response header and Serilog log context. Upload handler passes it into the job factory; worker sets it on the scope so the AI HTTP client can send it in headers. Enables tracing from upload → worker → AI.
- **Distributed safety:** No distributed locking or outbox. Claim is process-local. For multiple API instances, each has its own in-memory queue and worker; there is no cross-instance coordination. Scaling to multiple workers would require a shared queue and the same claim strategy (e.g. DB or queue message visibility).

---

# 5. AI Service Architecture

## FastAPI Service Structure

- **Entry:** `app/main.py` — FastAPI app with lifespan. Lifespan builds provider from settings and `get_provider_chain_names`, creates `ConceptService` and `QuizService`, stores them on `app.state`, logs the provider chain.
- **Routes:** `app/api/routes.py` — POST `/extract-concepts` (body: documentId, text) and POST `/generate-quiz` (body: documentId, concepts, question_count). Both use request-scoped service from `app.state`; no per-request provider switching.
- **Config:** `app/core/config.py` — Pydantic Settings from env: API keys (Gemini, DeepSeek, OpenRouter, OpenAI), model names, `LLM_FALLBACK_CHAIN`, request timeout.
- **Exception handling:** Global handler maps exceptions to JSON responses; httpx 429 is returned as 429 with optional Retry-After.

## Provider Abstraction and Multi-LLM Fallback

- **Base:** `LLMProvider` interface with `extract_concepts(text)` and `generate_questions(concepts, count)` returning list of dicts.
- **Implementations:** GeminiProvider, DeepSeekProvider, OpenRouterProvider, OpenAIProvider. Each uses its own API (Gemini REST, DeepSeek/OpenAI-compatible chat completions). Gemini uses short 429 retry (2 attempts, ~2s) then reraise so fallback can run; others retry only on 429/5xx (2 attempts, exponential 1–5s).
- **FallbackAdapter:** Holds an ordered list of providers (and names). For each call, tries the first; on any exception, unwraps `RetryError` to the underlying cause, logs, and tries the next. If all fail, re-raises the last error. So 429 or 5xx on Gemini quickly fails over to DeepSeek, then OpenRouter, then OpenAI if configured.
- **Chain building:** `get_provider` reads `LLM_FALLBACK_CHAIN` (e.g. "gemini,deepseek,openrouter,openai"); only providers with a non-empty API key are included. Single provider returns that instance; multiple return `FallbackAdapter(providers, names)`.

## Retry and Failover Logic

- **Gemini:** Tenacity retry on 429 only; 2 attempts; wait 2s; then reraise so FallbackAdapter can try next provider.
- **DeepSeek / OpenRouter:** Retry only if status is 429 or ≥500; 2 attempts; exponential backoff min 1s max 5s; 4xx (e.g. 404) are not retried.
- **Python:** No retry at route level; providers and adapter handle retries and failover. If all providers fail, FastAPI returns 500 or 503 (e.g. "Quiz generation produced no questions").

## DTO Boundaries and Contract

- **Request/response:** Pydantic models in `app/models/schemas.py`: ExtractConceptsRequest/Response, GenerateQuizRequest/Response, ConceptIn, QuizQuestionOut (text, options, correctAnswer). JSON uses camelCase aliases where defined.
- **.NET client:** `StudyPilotAIClient` sends documentId (string), text or concepts/questionCount; deserializes to `ConceptDto`, `GenerateQuizResultDto` (questions, promptVersion, modelName, etc.). No versioned URL path; header `X-Service-Version: v1` is sent for future use.
- **Contract versioning:** No explicit API version in URL. Backward compatibility is maintained by additive fields and default values. Breaking changes would require client and server rollout.

---

# 6. Frontend Architecture (Angular)

## Feature-Based Structure

- **Core:** Auth service (login, register, refresh, logout, token storage), HTTP interceptors (auth, error, loading, API response), guards (auth guard), config (environment token), document polling service, study-pilot API service, error banner and global error handler.
- **Features:** auth (login, register, routes), dashboard, documents (list, upload, routes), progress (weak-topics dashboard), quiz (quiz state service, quiz player component).
- **Shared:** Toast, API response model, toast service.

Standalone components and routes; `StudyPilotApiService` centralizes HTTP calls to the backend.

## API Integration Strategy

- **StudyPilotApiService:** Methods for auth, documents, quiz, progress, health. Returns observables; uses `HttpClient` with base URL from environment. All API responses are wrapped in `ApiResponse<T>` (success, data, errors, correlationId).
- **Interceptors (order):** Auth adds Bearer token; error interceptor handles 401 (refresh or redirect), 429 (toast), 503 (banner + toast), 5xx (toast); loading and API-response interceptors for UX and unwrapping.
- **Error handling:** Error interceptor maps status to refresh, logout, or toast/banner. Global error handler and error-banner service surface system errors with optional correlation ID.

## Auth Token Lifecycle

- Access token in localStorage; refresh token in sessionStorage. Keys: `study_pilot_access_token`, `study_pilot_refresh_token`, `study_pilot_expires_at`.
- On login/register, tokens and expiry are stored; a timeout is set to call refresh before expiry (e.g. 60s before). On 401, error interceptor tries refresh once; if the refreshed request is sent with `X-Skip-Refresh` to avoid loops. Failed refresh or no refresh token triggers logout and redirect to login.
- **Risks:** Access token in localStorage is readable by any script on the same origin (XSS). Refresh in sessionStorage is cleared when the tab closes.

## Polling and UI State

- **Document list:** After upload, the client can poll documents. `DocumentPollingService.pollUntilCompleted(intervalMs)` returns an observable that polls `getDocuments()` every N ms and completes when no document has status Pending or Processing (i.e. all Completed or Failed).
- **Quiz:** Start quiz returns quiz ID and count; quiz player requests questions by index. When status is Generating, UI can show loading and retry or poll; when Ready, show question and options; when Failed, show error. Next-question prefetch is triggered by the backend when returning the current question.

---

# 7. Database Design

## Main Entities and Relationships

- **Users:** Id, Email, PasswordHash, Role, CreatedAtUtc, UpdatedAtUtc. Unique on Email.
- **Documents:** Id, UserId, FileName, StoragePath, ProcessingStatus, FailureReason, CreatedAtUtc, UpdatedAtUtc. FK User.
- **Concepts:** Id, DocumentId, Name, Description, CreatedAtUtc, UpdatedAtUtc. FK Document.
- **Quizzes:** Id, DocumentId, CreatedForUserId, TotalQuestionCount, CreatedAtUtc, UpdatedAtUtc. FK Document, User.
- **Questions:** Id, QuizId, QuestionIndex, Text, QuestionType, CorrectAnswer, Status, GenerationAttempts, ErrorMessage, PromptVersion, ModelUsed, CreatedAtUtc, UpdatedAtUtc. FK Quiz. Unique (QuizId, QuestionIndex).
- **UserAnswers:** Id, UserId, QuestionId, SubmittedAnswer, IsCorrect, CreatedAtUtc, UpdatedAtUtc. FK User, Question.
- **UserConceptProgresses:** Id, UserId, ConceptId, MasteryScore, Attempts, LastReviewedUtc, CorrectCount (shadow), CreatedAtUtc, UpdatedAtUtc. Effectively unique (UserId, ConceptId) for lookups.
- **QuestionConceptLinks:** QuestionId, ConceptId. PK (QuestionId, ConceptId). Links a question to the concept it was generated from for mastery updates.
- **RefreshTokens:** Id, UserId, Token, ExpiresAtUtc, RevokedAtUtc, CreatedAtUtc. For refresh flow and logout.

## Indexing and Consistency

- Migrations define PKs and FKs. No additional indexes are defined in the initial migration; queries filter by UserId, DocumentId, QuizId, (QuizId, QuestionIndex), (UserId, ConceptId). For larger data, indexes on these columns and on ProcessingStatus, CreatedAtUtc would help.
- **Consistency:** Single DB; transactions via `SaveChangesAsync`. Claim uses `ExecuteUpdateAsync` (single statement). No eventual consistency model; read-your-writes within the same request/transaction.

## Mastery Model Logic

- **MasteryScore:** Stored as int 0–100. Value object enforces Increase(10) / Decrease(5) and clamping. Accuracy is computed as correctCount/Attempts (not stored; could be derived or cached).
- **Weak concepts:** Queries filter by low mastery and optionally sort by LastReviewedUtc. Result is cached under a user-scoped key and invalidated when the user submits a quiz.

---

# 8. Background Processing Design

## Queue Model

- **In-memory:** `Channel.CreateUnbounded<Func<CancellationToken, Task>>`. Jobs are delegates. No persistence, no delivery guarantees. Process restart loses all queued jobs.
- **Enqueue:** Synchronous; non-blocking unless channel is full (unbounded so effectively never).
- **Dequeue:** `ReadAsync` blocks until an item is available or cancellation. Single consumer.

## Worker Lifecycle

- **BackgroundJobWorker** runs as a HostedService. On startup it enters a loop: DequeueAsync → invoke job → catch, log, and increment failure metric. OperationCanceledException breaks the loop on shutdown. No graceful drain of remaining jobs; shutdown is immediate after the current job.
- **Cancellation:** The CancellationToken passed to the job is the worker’s stopping token. Long-running AI calls can be cancelled on app shutdown; document status is set to Failed with "Processing was cancelled." when possible.

## Concurrency Assumptions

- One worker process; one dequeuer. No competing consumers. Concurrency is only from multiple HTTP requests enqueueing jobs; processing is serialized per worker.
- **TryClaimForProcessingAsync:** Ensures only one processor "owns" a document for a given run. With multiple worker instances and a shared queue, the same document could be claimed by different instances if two jobs for the same document are in the queue; idempotency relies on the second claim seeing status already Processing and doing nothing.

## Future Scalability Path

- Replace in-memory channel with a durable queue (e.g. Azure Service Bus, RabbitMQ, SQS) and keep the same job payload (documentId + correlationId).
- Run multiple workers; each receives a message, runs the same job delegate with a new scope. Claim remains DB-based so only one processor can move a document from Pending to Processing.
- Optionally add visibility timeout / completion so that crashes don’t leave messages unprocessed forever.

---

# 9. Observability & Reliability

## Structured Logging

- Serilog is configured from appsettings. Enrich.FromLogContext() so CorrelationId and other properties are attached. Request logging middleware logs request and response. Application uses IRequestLogger (SerilogRequestLogger) in the MediatR pipeline. Worker and job factory log with DocumentId and CorrelationId.

## Correlation IDs

- **API:** CorrelationIdMiddleware reads X-Correlation-Id or generates a GUID; sets it on ICorrelationIdAccessor, response header, and LogContext. All downstream handlers and the AI client can add it to outbound requests (X-Correlation-Id) and logs.
- **Worker:** Job factory receives correlation ID from the upload handler and sets it on the scope’s accessor before running the job so the AI client and logs in the worker use the same ID.

## Health Checks

- **Liveness:** GET /health/live — 200 OK, no dependencies.
- **Startup:** GET /health/startup — 503 if DB cannot be connected.
- **Readiness:** GET /health/ready — DB connect, worker heartbeat (IsAlive: last update &lt; 60s), AI health. Returns 200 Ready, or 503 Unhealthy (DB/worker), or 200 Degraded (AI unhealthy).
- **AI health:** GET /health/ai — Cached 10s; calls AI service GET /health. Returns Healthy/Degraded/Unhealthy based on status code and latency (e.g. &gt;3s → Degraded).
- **Worker heartbeat:** WorkerHeartbeatService updates WorkerHeartbeatStore every 30s. Ready check fails if no update in 60s (single instance; if the worker is disabled or crashes, readiness fails).

## Resilience and Timeouts

- **HttpClient for AI:** Configured with timeout (e.g. 300s) and Polly AddStandardResilienceHandler: attempt timeout 300s, total request timeout 300s, circuit breaker disabled (ShouldHandle = false). So retries and timeouts are primarily on the AI service side (Python) and in the dispatcher (question generation retries).
- **Ownership:** API owns request timeouts (Kestrel, middleware); AI client owns HTTP timeouts; question generation owns retry count and backoff. Document processing job has no per-job timeout; it runs until completion or cancellation.

---

# 10. Security Model

## Authentication

- JWT access token (Bearer) for protected endpoints. Issued on login/register; contains sub (user id), email, role, exp. Refresh token is an opaque value stored in DB with expiry and revocation; used to issue new access (and optionally refresh) tokens.
- **Validation:** JWT validated on each request; invalid or expired token returns 401. Frontend stores access token and sends it on every API call; on 401 it attempts refresh then retry.

## Authorization

- Controllers use `[Authorize]` for protected routes. User id is taken from claims (e.g. NameIdentifier or "sub") via `User.GetCurrentUserId()`. No role-based policy is applied in the current controllers; document and quiz access are implicitly scoped by userId from the token (user can only see their documents and quizzes by construction of queries).

## Token Storage Risks

- Access token in localStorage is vulnerable to XSS; any script on the same origin can read it and call the API. Refresh token in sessionStorage is slightly better (tab-scoped) but still script-accessible. Best practice would be httpOnly cookies for refresh and short-lived access tokens, or a BFF that holds tokens server-side.

## Upload Validation and Path Traversal

- **UploadSecurity:** Rejects path traversal (.., /, \), double extensions, and invalid filename characters. Sanitizes to a single filename, max length 200. File size ≤10MB; content type allowed list (application/pdf, application/octet-stream, empty); PDF magic bytes checked before save.
- **Storage:** Files saved under `uploads/{userId}/{guid}_{sanitizedFileName}`. Path is stored in Document.StoragePath. When reading, LocalFileContentReader ensures the resolved path is under `StorageOptions.UploadsBasePath` to prevent escaping.

---

# 11. Configuration & Environment

## Environment Variables and Options

- **ConnectionStrings:Default** (or DefaultConnection) — PostgreSQL. Fallback in code: Host=localhost;Database=StudyPilot;Username=postgres;Password=postgres.
- **AIService:BaseUrl** — Python AI base URL (default http://study-pilot-ai:8000). **AIService:TimeoutSeconds** — HTTP timeout (default 300). **AIService:MaxTextLength** — max document text length for extraction (default 50_000). **AIService:QuestionGenerationRetryBaseDelayMs** — base delay for question generation retries (default 500).
- **UsageGuard:MaxDocumentsPerUserPerDay**, **MaxQuizGenerationPerHour** — limits (defaults 20, 30).
- **Jwt:Secret**, **Jwt:Issuer**, **Jwt:Audience**, **Jwt:AccessTokenExpirationMinutes**, **Jwt:RefreshTokenExpirationDays**.
- **Storage:UploadsBasePath** — optional; if set, file reads are restricted to this base path.
- **Python (study-pilot-ai/.env):** GEMINI_API_KEY, DEEPSEEK_API_KEY, OPENROUTER_API_KEY, OPENAI_API_KEY; LLM_FALLBACK_CHAIN (e.g. gemini,deepseek,openrouter,openai); optional model overrides and request timeout.

## Deployment Readiness

- appsettings.Production.json can override URLs and secrets. Kestrel max request body size configurable. Forwarded headers and CORS are configured; HSTS and cookie policy in production. No built-in health-check registration with a specific URL pattern beyond the custom HealthController routes.
- **Container readiness:** Dockerfile present for API; AI service and frontend can be containerized. Compose in deploy/ wires services; DB connection and AI base URL must be set for the environment. No explicit readiness probe configuration in the Dockerfiles beyond the app’s /health/ready.

---

# 12. Known Limitations (Critical Section)

| # | Problem | Why It Matters | Impact |
|---|--------|----------------|--------|
| 1 | **In-memory job queue** — Jobs are lost on process restart; no durability or replay. | Deployments or crashes lose pending document processing. Users see documents stuck in Pending. | **High** |
| 2 | **Single background worker** — One consumer; no horizontal scaling of processing. | Throughput is limited to one document at a time per API instance. | **High** |
| 3 | **No outbox or at-least-once delivery** — Enqueue is fire-and-forget. | If the process dies after SaveChanges but before or during Enqueue, the document may never be processed. | **Medium** |
| 4 | **Access token in localStorage** — Vulnerable to XSS. | Any XSS can steal the token and act as the user. | **High** |
| 5 | **No rate limit on AI service from .NET** — Only Python-side provider retries and failover. | A burst of uploads can overwhelm the AI service or LLM quotas; no backpressure at API level. | **Medium** |
| 6 | **Question generation race** — TryAddQuestionAsync + Dispatch; concurrent requests for same index can both insert (one wins) or both see Generating. | Duplicate work or transient 404/empty; generally self-healing but not strictly once-per-slot. | **Low** |
| 7 | **Worker readiness depends on heartbeat** — Single process; if the worker loop is blocked, heartbeat still ticks. | Readiness does not detect "worker stuck"; only process crash (no heartbeat) is detected. | **Medium** |
| 8 | **No DB indexes beyond PK/FK in initial migration** — Queries filter by UserId, DocumentId, CreatedAtUtc, etc. | At scale, list documents and weak-concepts queries can slow. | **Medium** |
| 9 | **File storage is local only** — No S3 or shared storage. | Multi-instance API cannot share uploads; worker must run where files are. | **High** for multi-instance |
| 10 | **AI contract not versioned in URL** — Client and server must stay in sync. | Rolling deployments or mixed versions can break if response shape changes. | **Medium** |
| 11 | **MasteryScore and Accuracy** — Accuracy derived from correctCount/Attempts; not stored. Weak-topics query may need to scan progress table. | Consistency is fine; performance and analytics may need tuning. | **Low** |
| 12 | **Refresh token in sessionStorage** — Lost when tab closes; user must re-login. | UX and session length are limited. | **Low** |
| 13 | **No audit log** — Who uploaded what when and who generated which quiz is only in application logs and DB rows. | Compliance or forensics may require additional audit trail. | **Medium** |
| 14 | **PDF text extraction only** — No images or complex layout. | Quality of extraction depends on PDF text layer. | **Low** (product choice) |

---

# 13. Improvement Roadmap

## Short-Term (MVP+)

- **Durable queue:** Introduce a real queue (e.g. Redis list, or cloud queue) for document jobs; worker dequeues and processes; on crash, message can be retried or dead-lettered. Preserves "API returns immediately" and improves reliability.
- **Health:** Register ASP.NET Core health checks (AddCheck) for DB and worker (e.g. heartbeat), and optionally AI; use UseHealthChecks and map to /health/ready for orchestrators.
- **Indexes:** Add indexes on Documents(UserId, CreatedAtUtc), UserConceptProgresses(UserId, MasteryScore), Quizzes(CreatedForUserId, CreatedAtUtc) to support list and weak-topics queries as data grows.

## Mid-Term (Scale)

- **Multiple workers:** Same code, multiple processes or containers consuming from the shared queue; document claim remains DB-based. Scale worker count with queue depth.
- **Shared storage:** Store uploaded files in object storage (S3-compatible); pass a URL or path that all instances and workers can read. Enables stateless API and worker scaling.
- **Token security:** Move refresh token to httpOnly cookie or BFF; shorten access token lifetime; consider PKCE if adding OAuth/OIDC later.
- **AI versioning:** Add /v1/ in AI routes and client path; allow /v2/ later without breaking existing clients.

## Long-Term (Enterprise)

- **Outbox pattern:** For document processing, write a row to an outbox table in the same transaction as Document insert; worker or relay reads outbox and enqueues to the durable queue. Guarantees at-least-once processing.
- **Audit and compliance:** Structured audit log (user, action, resource, timestamp, result) to a dedicated store; retention and access control.
- **Multi-tenancy or org scope:** If needed, introduce tenant/organization id in entities and queries; isolate data and enforce limits per tenant.
- **Resilience:** Circuit breaker and fallback for AI client (e.g. return 503 and ask user to retry); optional dead-letter queue and alerting on repeated failures.

---

# 14. Production Readiness Assessment

| Area | Score (1–10) | Explanation |
|------|--------------|-------------|
| **Architecture maturity** | 7 | Clean layers, CQRS, clear boundaries. Queue and storage are single-process/local; no distributed design yet. |
| **Scalability** | 5 | Single worker, in-memory queue, local files. Good for single-instance MVP; horizontal scaling blocked by queue and storage. |
| **Security** | 6 | Auth and upload validation in place; JWT and path checks. Token storage and lack of rate limit on AI are gaps. |
| **Observability** | 7 | Correlation ID, structured logs, health endpoints, metrics (Prometheus). No distributed tracing or centralized log aggregation in repo. |
| **Maintainability** | 8 | Clear naming, single responsibility, testable handlers and services. Domain and application are easy to change. |
| **AI reliability** | 7 | Multi-LLM fallback, retries, and failover reduce single-provider risk. No circuit breaker or backoff at API level; dependency on external LLM quotas. |

**Overall:** Suitable for a single-instance, internal or low-traffic deployment with awareness of the limitations above. Not yet suitable for high availability or multi-region without addressing queue, storage, and token storage.

---

# 15. Local Development Setup

**Prerequisites:** .NET 10 SDK, Node.js 18+, PostgreSQL 16, Python 3.12+, at least one LLM API key (e.g. Gemini).

**Database (from repo root):**
```bash
psql -U postgres -f scripts/postgres-setup.sql
# If DB/user exist: psql -U postgres -d StudyPilot -f scripts/postgres-grant-public.sql
```

**Backend API:**
```bash
cd src/StudyPilot.API
# Set ConnectionStrings__Default and Jwt__Secret in appsettings.Development.json
dotnet run
```
Runs on port from launchSettings (e.g. 5024).

**AI service:**
```bash
cd study-pilot-ai
pip install -e .
cp .env.example .env   # Set at least GEMINI_API_KEY (or other provider keys)
uvicorn app.main:app --reload --host 0.0.0.0 --port 8000
```

**Frontend:**
```bash
cd study-pilot-app
npm install
npm start
```
Runs at http://localhost:4200; proxy to API per proxy.conf.json.

**Optional:** Use `scripts/run-local.ps1` to start AI, API, and frontend in separate windows. Ensure API `AIService:BaseUrl` points to http://localhost:8000 and frontend proxies /api to the API URL.

---

# 16. Deployment Strategy (Conceptual)

## Expected Topology

- **Single region:** One or more API instances behind a load balancer; one or more worker instances consuming from a shared queue; one AI service deployment (or scaled replicas behind a single URL); one PostgreSQL primary; shared file store if multiple API/worker instances.
- **Current state:** Single API + single in-process worker + local disk fits one VM or one container per service (API, AI, frontend); DB as a service or container. No shared queue or shared storage yet.

## Scaling Strategy

- **API:** Stateless; scale out behind LB. Requires shared storage for uploads and a durable queue so workers (separate processes) can process jobs. Without that, only one API instance can own the in-memory queue and local files.
- **Workers:** Scale by increasing consumer count on the same queue; document claim in DB ensures each document is processed once. Optionally partition by document ID or user for affinity.
- **AI service:** Scale replicas; stateless. Rate limits are per API key; multiple replicas share the same key unless keys are sharded.
- **DB:** Vertical scale first; read replicas for GetDocuments and weak-topics if needed. Migrations run from one place (e.g. API startup or a dedicated job).

## Worker Scaling Model

- Today: One BackgroundService per API process. To scale workers independently, run a separate worker process/container that shares the same DB and (once introduced) the same queue and storage. No code change to the job logic; only the host and queue implementation change.

---

*End of engineering documentation.*
