You are implementing STEP 10 — Production Hardening
for the StudyPilot system.

IMPORTANT:
Generate ONLY production code and configuration.
NO README files.
NO explanations.
NO tutorial comments.
DO NOT change architecture or folder structure.
Extend existing system safely.

====================================================
EXISTING SYSTEM (ALREADY COMPLETE)
====================================================

Backend:
- .NET 10 LTS
- Clean Architecture
- CQRS (MediatR)
- EF Core + PostgreSQL
- Background worker
- AI HTTP integration
- CorrelationId propagation
- Dockerized deployment
- JWT + Refresh tokens

Frontend:
- Angular (latest LTS)
- Standalone architecture

AI Service:
- FastAPI microservice

Goal now:
Harden system for real production usage.

====================================================
HARDENING PILLAR 1 — OBSERVABILITY
====================================================

Add OpenTelemetry support.

Install packages and configure:

- tracing
- metrics
- logging correlation

Instrument:

- ASP.NET requests
- HttpClient calls
- EF Core queries
- Background worker jobs

Exporters:
- OTLP exporter (configurable)
- Console exporter fallback

Create metrics:

documents_processed_total
ai_request_duration_ms
background_queue_length
quiz_generation_duration_ms

Expose endpoint:

GET /metrics

Prometheus compatible.

====================================================
HARDENING PILLAR 2 — RATE LIMITING
====================================================

Use ASP.NET built-in rate limiting middleware.

Policies:

auth-policy:
  10 requests/minute

upload-policy:
  5 requests/minute

quiz-policy:
  20 requests/minute

Apply policies per endpoint:

/auth/*
/documents/upload
/quiz/start

Return proper 429 responses.

====================================================
HARDENING PILLAR 3 — CACHE ABSTRACTION
====================================================

Create abstraction:

Application/Abstractions/Caching/ICacheService

Methods:

GetAsync<T>()
SetAsync<T>()
RemoveAsync()

Infrastructure implementation:

MemoryCacheService using IMemoryCache.

Register via DI.

Use cache for:

- weak topics query (short TTL)
- AI health check (10s TTL)
- document list (short TTL)

DO NOT cache writes.

Design must allow Redis replacement later.

====================================================
HARDENING PILLAR 4 — DATABASE SAFETY
====================================================

Startup migration safety:

On application start:

- attempt db.Database.MigrateAsync()
- retry 3 times with delay.

Add missing indexes if not present:

Documents(UserId)
Concepts(DocumentId)
UserConceptProgress(UserId, ConceptId)
Documents(ProcessingStatus)

All via Fluent API configurations.

====================================================
HARDENING PILLAR 5 — AI FAILURE & COST CONTROL
====================================================

Enhance HttpClient resilience:

Configure circuit breaker using
AddStandardResilienceHandler options.

Behavior:

- repeated AI failures open circuit
- short cooldown window
- fast failure during open state

Add application guard:

IUsageGuardService

Limits configurable:

MaxDocumentsPerUserPerDay
MaxQuizGenerationPerHour

Enforce inside command handlers.

====================================================
HARDENING PILLAR 6 — SECURITY HARDENING
====================================================

Add SecurityHeadersMiddleware.

Headers:

X-Content-Type-Options: nosniff
X-Frame-Options: DENY
Referrer-Policy: no-referrer
Content-Security-Policy minimal safe default

Register early in pipeline.

----------------------------------------------------

Upload validation improvements:

- verify MIME type
- verify file signature (PDF magic bytes)
- reject invalid payloads safely.

----------------------------------------------------

Logging safety:

Ensure sensitive fields never logged:

- JWT tokens
- passwords
- document text

Add log filtering where needed.

====================================================
PERFORMANCE RULES
====================================================

- No blocking calls.
- Fully async.
- CancellationToken propagated.
- No static state.
- DI only.

====================================================
OUTPUT RULE
====================================================

Generate ONLY:

- middleware
- services
- DI registrations
- configuration updates
- metric setup
- rate limiting setup
- cache implementation
- guards/services
- index configs

NO README.
STOP after implementation completes.