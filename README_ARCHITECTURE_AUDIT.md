# StudyPilot Architecture Audit

## Critical Bottlenecks (Fix First)

**Document file read twice in pipeline**  
Impact: Double disk I/O and latency for every document; embedding job runs after concept extraction but re-reads the same file.  
Why: DocumentProcessingJobFactory reads file for extract-concepts; KnowledgeEmbeddingJobFactory reads again for chunking/embedding. No handoff of content or chunk list.  
Fix: Pass extracted text or chunk list from document job to embedding job (e.g. via shared cache keyed by documentId with TTL), or have a single job that does extract → persist → chunk → embed in one scope and only enqueue embedding if a separate worker is required.

**BackgroundJobRepository.TryClaimNextAsync double round-trip**  
Impact: Every job claim = 2 DB round-trips (transaction with raw SQL for id, then separate FirstOrDefaultAsync to load full entity).  
Why: Claim uses raw SQL returning only Id; then job entity is loaded in a second query.  
Fix: Return required fields in raw SQL or use a single query that returns the row and map to entity/DTO so one round-trip suffices.

**GetPendingCountAsync on every empty poll**  
Impact: When queue is idle, every poll interval triggers an extra COUNT query on BackgroundJobs in addition to the claim query.  
Why: BackgroundJobWorker calls GetPendingCountAsync when TryClaimNextAsync returns null, for logging.  
Fix: Log pending count only at debug or at a throttled interval (e.g. every Nth empty poll), or remove and rely on metrics.

**StudyPilotAIClient ReadAsStringAsync + manual deserialize**  
Impact: Response body buffered as string then parsed again; extra allocation and CPU vs streaming deserialize.  
Why: ExtractConceptsAsync and GenerateQuizAsync use ReadAsStringAsync then JsonExtractor.Deserialize.  
Fix: Use response.Content.ReadFromJsonAsync&lt;T&gt; with cancellation (and same JSON options) to deserialize once from stream.

**ProcessSync=true blocks request for full document processing**  
Impact: If UploadDocumentCommand is ever called with ProcessSync=true, the HTTP request holds for entire extract-concepts LLM call + DB writes.  
Why: UploadDocumentCommandHandler runs _jobFactory.CreateProcessDocumentJob(...) and awaits it inline when request.ProcessSync is true.  
Fix: Remove ProcessSync path or document that it is for admin/testing only and must not be used in production; ensure API never sends ProcessSync true (DocumentsController currently passes false).

**Circuit breaker never opens**  
Impact: Resilience handler retries and timeouts apply but circuit never breaks; repeated failures still hit the AI service.  
Why: AddStandardResilienceHandler sets CircuitBreaker.ShouldHandle = _ => new ValueTask&lt;bool&gt;(false).  
Fix: Set ShouldHandle to true for HttpRequestException, TimeoutException, or 5xx responses so circuit can open after threshold.

**Document list polling fetches all documents**  
Impact: Front-end document polling (every 5s) calls getDocuments() which returns full list for user; no filter by status or document id.  
Why: DocumentPollingService.pollUntilCompleted() uses api.getDocuments() and checks client-side if any doc has status Pending/Processing.  
Fix: Add API support for “documents with status Pending or Processing” or “document status by id” and have UI poll only that (or single doc when waiting for one).

**LearningInsightGenerator N+1 and sequential user loop**  
Impact: For each user, loads all masteries then for each candidate insight calls ExistsAsync (2 queries per candidate). Many users × many masteries = large query volume.  
Why: foreach userId → GetByUserIdAsync → foreach mastery → ExistsAsync for RepeatedMistake and Improvement.  
Fix: Batch existence checks (e.g. single query that returns existing (userId, conceptId, type) in cooldown window) or bulk-insert with conflict ignore and let DB enforce uniqueness.

**BackgroundJobs missing index on DocumentId**  
Impact: ReleaseStuckJobs recovery and ExistsPendingOrProcessingForDocumentAsync filter by DocumentId; without index these can scan.  
Why: BackgroundJobConfiguration only has indexes on Status and (Status, NextRetryAtUtc), (Status, CreatedAtUtc).  
Fix: Add index on DocumentId (and optionally (DocumentId, Status) if often filtered by both).

**AI generate-quiz retry doubles LLM call**  
Impact: When first call returns 0 questions, Python route retries with question_count=1; two full LLM calls for one quiz.  
Why: routes.py generate_quiz: if not questions and count > 1, calls generate_questions again with 1.  
Fix: Retry only on transient failure (e.g. 503/timeout); if LLM returns valid empty/invalid JSON, fail fast or single retry with same params.

---

## Performance Issues

- QuizQuestionGenerationJobWorker: 4–5 sequential DB calls per job (release stuck, claim, get question, get quiz, get concept order, get concept by id or by document).
- Quiz worker uses hardcoded 30s LLM timeout (LlmTimeoutSeconds) while job processing timeout is configurable (minutes); mismatch can mark job failed before processing timeout.
- DocumentRepository.TryClaimForProcessingAsync: ExecuteUpdateAsync then FirstOrDefaultAsync — two round-trips; could use RETURNING or output from update.
- SendChatMessageCommandHandler: three SaveChangesAsync (user message, assistant message, citations); could batch into one.
- ResolveExplanationStyleAsync: two DB calls per chat message (GetByDocumentIdAsync concepts, GetByUserAndConceptsAsync masteries); could be combined or cached per session.
- LearningIntelligenceWorker: ApplyTimeDecayAsync per user sequentially; no batching or parallelization.
- LearningIntelligenceWorker uses CreateScope() not CreateAsyncScope(); minor for DB but inconsistent with rest of codebase.
- DocumentProcessingJobFactory uses CreateScope() not CreateAsyncScope(); on failure/cancel calls SaveChangesAsync(CancellationToken.None) — ignores cancellation.
- KnowledgeEmbeddingJobFactory: SaveChangesAsync after every batch of 32 chunks; more round-trips than necessary (e.g. batch several batches in one transaction).
- PgVectorSearchService uses DbContext’s connection for raw ADO; ensure no concurrent use of same DbContext elsewhere in the request or scope.

---

## Reliability Risks

- GetQuizQuestionQueryHandler: fire-and-forget _quizJobQueue.EnqueueAsync(..., CancellationToken.None) for next question; enqueue failure is unobserved; no backpressure if user skips questions (many jobs can queue).
- Document processing and embedding job failure paths: SaveChangesAsync(CancellationToken.None) on cleanup can mask cancellation and run after request/worker shutdown.
- KnowledgeEmbeddingJobRepository.TryClaimNextAsync: catch (Exception) then RollbackAsync and rethrow; no logging — failures are opaque.
- BackgroundJobWorker catch (Exception) in poll loop: logs and continues; good, but MarkFailedAsync for job can fail and leave job in Processing state until ReleaseStuckJobs (next cycle).
- No explicit health for job queues (depth, age); only WorkerHeartbeatService for worker liveness.
- AI service: if embedding API key missing, returns zero vectors (embedding_service.py); chunks are stored with zero vectors and search quality degrades without clear failure signal.

---

## Scalability Limits

- Single worker per process for each job type (BackgroundJobWorker, QuizQuestionGenerationJobWorker, KnowledgeEmbeddingJobWorker); horizontal scaling = multiple processes with same DB queue (claim by worker id); no in-process parallelism per worker.
- AiConcurrencyHandler: global SemaphoreSlim limits concurrent AI HTTP calls (MaxConcurrentRequests); shared across document, quiz, chat, tutor; one slow LLM can block others.
- Embedding: chunks processed in batches of 32; one document with many chunks makes many sequential embed calls; no parallel batching across chunks.
- LearningIntelligenceWorker: one 6-hour loop for all users; time decay and insight generation are sequential per user; will not scale to large user counts without batching or sharding.
- Document polling: every 5s full document list; with many documents and many concurrent users, GET /documents load grows with no server-driven or document-scoped refresh.

---

## Overengineering Findings

- **IDocumentProcessingJobFactory / IKnowledgeEmbeddingJobFactory**: Single implementation each; factory returns Func&lt;CancellationToken, Task&gt;. No variability (e.g. different job types). Simpler: inject a service that exposes RunDocumentProcessingAsync(documentId, correlationId, jobId, ct) and RunEmbeddingAsync(documentId, correlationId, ct) and call it from workers.
- **Circuit breaker configuration with ShouldHandle = false**: Full resilience options (timeouts, retries, sampling) are set but circuit never triggers. Either enable (set ShouldHandle for relevant failures) or remove circuit breaker config to avoid confusion.
- **CQRS/MediatR for every command and query**: Acceptable for consistency; some handlers are thin (e.g. GetStudySuggestions) and could be simple controller → service if team prefers less indirection.
- **ChaosSimulationOptions and ApplyChaosAsync in StudyPilotKnowledgeAIClient**: Only used for testing; ensure not enabled in production (config/env) or gate behind feature flag.

---

## Hidden Latency Multipliers

- Chat: embedding cache lookup + possible embed call + hybrid search + ResolveExplanationStyle (2 queries) + LLM call + 3× SaveChanges; each message pays full chain.
- Tutor: similar pattern (retrieve context, LLM); tutor/stream endpoint exists in backend client but route in Python may need verification for /tutor/stream.
- Quiz question: each question = 1 LLM call (GenerateQuestionAsync → GenerateQuizAsync with count 1); N questions = N calls; no batch generate in worker.
- Correlation ID: set in scope in job factories; if not set from HTTP context in worker, logs may lack correlation for background jobs (worker sets from job payload when present).
- ReleaseStuckJobsAsync runs every poll for all three workers; under load this is 3× UPDATEs per poll interval even when no work.

---

## Observability Gaps

- No metrics for queue depth (pending job count per queue) or job age; only job completion/failure counters.
- Background job and quiz job claim failure: only debug log when no job; no metric for “empty poll” rate.
- LearningIntelligenceWorker: no metric for “insights generated this run” or “users processed”; only LearningInsightsGenerated.Add(insights.Count) when insights &gt; 0.
- Request-scoped correlation ID may not propagate to background job logs if not set from job payload in factory (DocumentProcessingJobFactory and KnowledgeEmbeddingJobFactory do set from correlationId parameter).
- No distributed tracing or span for “document processing” or “embedding job” as a single unit across API → worker → AI service.
- SerilogRequestLogger: thin wrapper; no structured fields for duration, status code, or endpoint in one place (may be in middleware; not verified).

---

## Top 10 Highest ROI Fixes

1. Add index on BackgroundJobs.DocumentId (and optionally (DocumentId, Status)).
2. Remove or throttle GetPendingCountAsync on empty poll in BackgroundJobWorker.
3. StudyPilotAIClient: switch to ReadFromJsonAsync instead of ReadAsStringAsync + JsonExtractor.Deserialize.
4. Eliminate duplicate document read: pass text or chunks from document job to embedding job (or single pipeline step).
5. TryClaimNextAsync: return job in one round-trip (e.g. raw SQL returning needed columns or single EF query with FOR UPDATE SKIP LOCKED).
6. Enable circuit breaker: set ShouldHandle for HTTP/timeout/5xx so circuit can open under repeated AI failures.
7. Document polling: add API to get “pending/processing” documents or status by document id; front-end polls that instead of full list.
8. LearningInsightGenerator: batch ExistsAsync checks or use single query for “existing insights in cooldown” and avoid per-candidate round-trips.
9. Ensure ProcessSync is never used in production and/or remove sync path from UploadDocumentCommandHandler.
10. Log or observe EnqueueAsync for next-question prefetch in GetQuizQuestionQueryHandler; consider backpressure (e.g. only enqueue next if queue depth below threshold).
