====================================================
PRODUCTION GUARANTEES (MANDATORY)
====================================================

1. Idempotency
- Processing the same Document multiple times MUST NOT create duplicate Concepts.
- Implementation must be safe for retries or duplicate job execution.

2. Distributed Safety
- Design must remain correct if multiple API or worker instances run simultaneously.
- No assumptions about single-instance execution.

3. Correlation & Observability
- Every request generates a CorrelationId.
- CorrelationId must flow:
  API → Background Job → AI Service → Logs.
- Send header:
  X-Correlation-Id

4. AI Contract Versioning
- Every AI HTTP request must include header:
  X-Service-Version: v1

5. Timeout Ownership
- HTTP timeout controlled ONLY via AIServiceOptions.
- Background worker must not impose additional timeouts.

6. Cancellation Semantics
CancellationToken MUST:
- cancel outbound HTTP calls
- stop worker gracefully
- NOT corrupt persisted state.

7. DTO Ownership
- AI DTOs exist ONLY inside Infrastructure layer.
- Application layer must remain AI-agnostic.

8. Payload Safety
- Maximum AI input size configurable.
- Oversized documents must fail safely before AI call.

9. Worker Concurrency
- Background worker processes ONE job at a time (MVP).
- Design must allow future parallelization.

10. Health Classification
AI health endpoint must return:
- Healthy
- Degraded (timeout/high latency)
- Unhealthy (unreachable)