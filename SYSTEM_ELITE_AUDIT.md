# StudyPilot Elite System Audit

## 🔴 Production Failure Risks

- **Issue:** Circuit breaker never opens — `ShouldHandle = _ => new ValueTask<bool>(false)`.
  **Location:** `DependencyInjection.cs` (both HttpClient registrations, AddStandardResilienceHandler).
  **Why it fails at scale:** Failures are never classified; circuit never trips; failing AI keeps getting full traffic.
  **Minimal Fix:** Set `ShouldHandle` to treat `HttpRequestException`/`TaskCanceledException`/5xx as handleable.

- **Issue:** PDF read is synchronous inside `ReadAllTextAsync` (PdfDocument.Open + GetPages loop).
  **Location:** `LocalFileContentReader.cs` (PDF branch).
  **Why it fails at scale:** Blocks thread-pool threads for entire PDF parse; under load causes thread starvation and request queue growth.
  **Minimal Fix:** Run PDF open/read on `Task.Run` or use async-capable PDF API with true async I/O.

- **Issue:** Fire-and-forget enqueue with `CancellationToken.None`; exception is unobserved.
  **Location:** `GetQuizQuestionQueryHandler.cs` line 64: `_ = _quizJobQueue.EnqueueAsync(..., CancellationToken.None)`.
  **Why it fails at scale:** Enqueue failures (e.g. DB timeout) are lost; next question never queued; quiz appears stuck.
  **Minimal Fix:** Await enqueue or register continuation with logging; pass request `CancellationToken` if applicable.

- **Issue:** Document processing job uses `CreateScope()` (sync) not `CreateAsyncScope()`; cancellation uses `SaveChangesAsync(CancellationToken.None)` on failure path.
  **Location:** `DocumentProcessingJobFactory.cs` (CreateProcessDocumentJob: scope, catch blocks).
  **Why it fails at scale:** Sync scope disposal can block; CancellationToken.None on save hides shutdown/request cancel and can delay process exit.
  **Minimal Fix:** Use `CreateAsyncScope()` and pass `ct` (or linked token) into SaveChanges in catch.

- **Issue:** Knowledge embedding worker retries on every exception (including validation/parse), not only transient.
  **Location:** `KnowledgeEmbeddingJobWorker.cs` (catch block: allowRetry = job.RetryCount + 1 < maxRetries for all ex).
  **Why it fails at scale:** Poison jobs (e.g. dimension mismatch, bad data) retry indefinitely; queue backs up; same job fails repeatedly.
  **Minimal Fix:** Restrict allowRetry to `HttpRequestException`, `TimeoutException`, `OperationCanceledException` (mirror BackgroundJobWorker).

- **Issue:** Background job claim order is LIFO (`ORDER BY CreatedAtUtc DESC`).
  **Location:** `BackgroundJobRepository.cs` TryClaimNextAsync.
  **Why it fails at scale:** New jobs are claimed first; old documents can starve under sustained uploads.
  **Minimal Fix:** Use `ORDER BY CreatedAtUtc ASC` (FIFO) for fairness.

- **Issue:** In-memory `_pendingCountApprox` is process-local; DecrementPendingCount only in worker that processes.
  **Location:** `DbBackedBackgroundJobQueue.cs` (QueuedCount, DecrementPendingCount).
  **Why it fails at scale:** Multi-instance deployment: each node has wrong local count; metrics/backpressure decisions are incorrect.
  **Minimal Fix:** Expose queue depth from DB (e.g. GetPendingCountAsync) for metrics/alerting; document as single-node only or add distributed counter.

- **Issue:** Ollama provider retries with tenacity (3 attempts, exponential 1–10s) but no jitter.
  **Location:** `study-pilot-ai/app/providers/ollama_provider.py` (_chat @retry).
  **Why it fails at scale:** Many workers retry at same intervals → thundering herd on Ollama; 503/overload can persist.
  **Minimal Fix:** Add jitter to wait (e.g. wait_random_exponential) or use retry_after from response if present.

- **Issue:** AI startup check only uses IStudyPilotAIClient; Knowledge client (chat/embed/tutor) is not verified.
  **Location:** `AIStartupCheck.cs` (CheckHealthAsync via IStudyPilotAIClient).
  **Why it fails at scale:** Extract/quiz may pass while chat/embed/tutor endpoint is down; first user request fails.
  **Minimal Fix:** Run health check against the same base URL used by Knowledge client or add a lightweight ping to shared health.

- **Issue:** Quiz worker uses hardcoded 30s LLM timeout; adapter calls GenerateQuizAsync(1 question) so full generate-quiz pipeline runs per question.
  **Location:** `QuizQuestionGenerationJobWorker.cs` (LlmTimeoutSeconds = 30); `StudyPilotAIServiceAdapter.GenerateQuestionAsync` calls GenerateQuizAsync(documentId, [concept.Name], 1).
  **Why it fails at scale:** Single Ollama handles one request at a time; N questions = N serial HTTP calls; 30s × N can exceed job processing window; no dedicated single-question endpoint increases tokens/call.
  **Minimal Fix:** Add a single-question AI endpoint and call it from adapter; or increase worker job timeout and ensure Ollama concurrency/config can handle queue.

- **Issue:** EF Core `EnableRetryOnFailure(3, TimeSpan.FromSeconds(2), null)` has no jitter.
  **Location:** `DependencyInjection.cs` (UseNpgsql).
  **Why it fails at scale:** All retrying connections back off identically; transient DB blips can cause synchronized retry storms.
  **Minimal Fix:** Use overload that accepts delay strategy with jitter if available, or custom execution strategy with jitter.

## 🟠 Latency Amplifiers

- **Issue:** Per-question generation uses full generate-quiz API (1 concept, 1 question) → full prompt + JSON parse path per question.
  **Location:** `StudyPilotAIServiceAdapter.GenerateQuestionAsync` → `_client.GenerateQuizAsync(..., 1)`.
  **Why it amplifies:** Repeated prompt overhead and response parsing; Python may retry with count=1 on empty (second LLM call). At 5 questions, 5–10 LLM calls.
  **Minimal Fix:** Dedicated single-question endpoint and client method to cut prompt/parse and retry scope.

- **Issue:** Chat request path: query embed → vector search → keyword search → concept/mastery load → LLM chat; all sequential.
  **Location:** `StreamChatMessageQueryHandler` + `HybridSearchService.SearchAsync` + `ChatService.StreamChatAsync`.
  **Why it amplifies:** Latencies add; no parallelization of vector vs keyword or of embedding cache miss vs search.
  **Minimal Fix:** Run vector and keyword search in parallel where safe; consider parallelizing independent steps.

- **Issue:** Embedding job runs batches of 32 sequentially (EmbedBatchAsync → AddRange → SaveChanges per batch).
  **Location:** `KnowledgeEmbeddingJobFactory.cs` (for loop over chunks, batchSize 32).
  **Why it amplifies:** One slow batch (e.g. AI latency) blocks the rest; total time = sum of batch times; shared semaphore with chat/quiz.
  **Minimal Fix:** Keep batching but consider parallel batches up to concurrency limit; or increase batch size if AI allows and dimension matches.

- **Issue:** Document pipeline: ReadAllText (sync for PDF) → ExtractConcepts (LLM) → DeleteConcepts + AddConcepts + 3× SaveChanges → Enqueue embedding; all on request thread when ProcessSync=true.
  **Location:** `UploadDocumentCommandHandler` (ProcessSync branch); `DocumentProcessingJobFactory` (full pipeline).
  **Why it amplifies:** Sync PDF read + LLM + multiple DB round trips; if ProcessSync used, request holds for entire pipeline.
  **Minimal Fix:** Avoid ProcessSync for large docs; or ensure it’s off in production and document as dev-only.

- **Issue:** StudyPilotKnowledgeAIClient has no per-request timeout override; only HttpClient 120s.
  **Location:** `StudyPilotKnowledgeAIClient.cs` (ChatAsync, CreateEmbeddingsAsync, TutorRespondAsync, etc.).
  **Why it amplifies:** Long-running LLM calls hold semaphore and connection for up to 120s; one slow request blocks others up to MaxConcurrentRequests.
  **Minimal Fix:** Apply per-call timeout (CancellationTokenSource.CancelAfter) using config (e.g. LlmTimeoutSeconds) as for StudyPilotAIClient.

- **Issue:** LearningIntelligenceWorker runs time decay per user sequentially (foreach userId ApplyTimeDecayAsync).
  **Location:** `LearningIntelligenceWorker.cs` (foreach userId).
  **Why it amplifies:** Many users → long single run; one slow user or DB stalls the whole cycle.
  **Minimal Fix:** Batch users or process in parallel with bounded concurrency; add per-user timeout.

## 🟡 Overengineering Hurting Throughput

- **Issue:** Two separate HTTP clients (IStudyPilotAIClient vs IStudyPilotKnowledgeAIClient) for same AI service base URL with separate resilience and timeout configs.
  **Location:** `DependencyInjection.cs` (AddHttpClient for both).
  **Why it hurts:** Duplicate handler chains, two timeouts to tune (300s vs 120s), same semaphore shared but config drift risk; no single backpressure/observability story.
  **Minimal Fix:** Unify to one client or one base URL config and shared timeout/retry for all AI calls.

- **Issue:** QuestionGenerationDispatcher in-process retries (MaxRetries 3) with DB updates per attempt while quiz worker also retries at job level.
  **Location:** `QuestionGenerationDispatcher.DispatchAsync` (for attempt, UpdateQuestionAsync, SaveChanges, then GenerateQuestionAsync).
  **Why it hurts:** Dispatcher is used only from in-process path; worker path uses job retries. Double retry layers when dispatcher is used; extra DB writes per attempt.
  **Minimal Fix:** Use dispatcher only where no worker exists (e.g. sync flow); otherwise enqueue and let worker own retries; avoid duplicate retry layers.

- **Issue:** JsonExtractor.Deserialize returns null on any parse failure; caller must handle null; no distinction between empty and parse error.
  **Location:** `JsonExtractor.cs` (Deserialize catch returns null).
  **Why it hurts:** ExtractConcepts/GenerateQuiz receive null and treat as empty; parse errors (e.g. truncation) look like “no concepts” and may trigger unnecessary retries or wrong user message.
  **Minimal Fix:** Return result type (success + list vs parse error) or throw on parse so callers can retry or report accurately.

- **Issue:** Worker heartbeat is in-process only (WorkerHeartbeatStore); no distributed view of workers.
  **Location:** `WorkerHeartbeatService.cs`, `WorkerHeartbeatStore.cs`.
  **Why it hurts:** In multi-instance deployments, health shows “alive” per instance with no notion of which instance is processing which queue; can’t detect “all workers stuck” across nodes.
  **Minimal Fix:** Either document as single-node only or add a shared store (e.g. DB or cache) for heartbeats and queue ownership for observability.

## 🔵 Observability Blind Spots

- **Issue:** No metric or log for quiz-question job queue depth or embedding job queue depth; only doc-processing queue exposed via BackgroundQueueLength.
  **Location:** `StudyPilotMetrics.cs` (SetQueueLengthProvider only for IBackgroundQueueMetrics); quiz and embedding queues have no gauge.
  **Why it fails:** Can’t answer “how many quiz/embedding jobs are pending”; backpressure and SLA debugging are blind.
  **Minimal Fix:** Register observable gauges (or equivalent) for quiz and embedding pending counts from their repositories.

- **Issue:** No span or metric that ties “which step failed” in document or quiz pipeline (e.g. extract vs persist vs embed enqueue).
  **Location:** Document pipeline in `DocumentProcessingJobFactory`; quiz pipeline in `QuizQuestionGenerationJobWorker`.
  **Why it fails:** Logs are present but no structured “step” metric or trace attribute; hard to aggregate “failures at step X” in dashboards.
  **Minimal Fix:** Emit a metric (e.g. counter with step label) or trace span per major step (extract, persist, enqueue, embed) on failure.

- **Issue:** LLM duration is logged (StepComplete) but not consistently as a metric with operation/endpoint label; AIRequestDurationMs is generic.
  **Location:** `StudyPilotAIClient` (log only); `StudyPilotKnowledgeAIClient` (Record AIRequestDurationMs but no operation name).
  **Why it fails:** Can’t break down “where time is spent” by operation (extract vs generate-quiz vs chat vs embed) from metrics alone.
  **Minimal Fix:** Record histogram with label (e.g. operation: extract_concepts, generate_quiz, chat, embeddings) or equivalent.

- **Issue:** Retry count and next retry time are not exposed as metrics; only in logs.
  **Location:** All workers (Background, Quiz, KnowledgeEmbedding) when MarkFailedAsync(allowRetry, nextRetry).
  **Why it fails:** Can’t alert on “high retry rate” or “many jobs waiting for next retry” without parsing logs.
  **Minimal Fix:** Increment a “job_retries_total” (or similar) counter with queue type and optionally allowRetry; optional gauge for “jobs waiting retry”.

- **Issue:** Python AI service: no structured metrics for request duration, parse failures, or Ollama retries.
  **Location:** `study-pilot-ai` routes and `ollama_provider.py`.
  **Why it fails:** .NET only sees HTTP success/failure; can’t tell “slow LLM” vs “parse failed after LLM” vs “Ollama retry storm”.
  **Minimal Fix:** Add metrics (e.g. Prometheus) in Python for request duration, parse errors, and retry count; or propagate in response headers for .NET to record.

## ⚡ Highest ROI Fixes (Top 10 Only)

- Enable circuit breaker: set `ShouldHandle` to true for transient HTTP/exceptions so circuit can open under AI failure.
- Fix sync PDF read: run PDF parsing off request/worker thread (e.g. `Task.Run`) or use async PDF API to avoid thread-pool starvation.
- Await or observe quiz next-question enqueue: avoid fire-and-forget with `CancellationToken.None`; log and optionally pass `ct`.
- Use FIFO for background job claim: change `ORDER BY CreatedAtUtc DESC` to `ASC` in BackgroundJobRepository.
- Restrict embedding worker retries to transient failures only (e.g. HTTP, timeout, cancel), not validation/parse.
- Add per-request timeout for Knowledge client (e.g. LlmTimeoutSeconds + CancelAfter) so one slow call doesn’t hold slot for 120s.
- Add single-question AI endpoint and use it in adapter to reduce latency and token waste for per-question generation.
- Expose quiz and embedding queue depth in metrics (gauges from DB pending counts).
- Add jitter to Ollama retries (Python) and to EF Core retry delay (or custom strategy) to avoid thundering herd.
- Use `CreateAsyncScope()` and pass cancellation into SaveChanges in document job failure path; avoid `CancellationToken.None` on save.
