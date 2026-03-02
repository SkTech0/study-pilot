You are implementing the FINAL LAUNCH CHECKLIST
for the StudyPilot production system.

This step DOES NOT add new features.
It verifies, safeguards, and prepares the system
for public internet exposure.

Generate ONLY code and configuration.
NO README files.
NO explanations.
NO tutorial comments.
DO NOT modify architecture.

Goal:
System must be safe, observable, recoverable, and deployable.

====================================================
EXISTING SYSTEM (ALREADY COMPLETE)
====================================================

Backend:
- .NET 10 LTS
- Clean Architecture
- CQRS + MediatR
- PostgreSQL
- Background workers
- AI integration
- OpenTelemetry
- Rate limiting
- Cache abstraction
- Security headers
- Dockerized deployment

Frontend:
- Angular LTS production build

AI Service:
- FastAPI container

====================================================
LAUNCH CHECKLIST REQUIREMENTS
====================================================

----------------------------------------------------
1. CONFIGURATION VALIDATION (FAIL FAST)
----------------------------------------------------

Create StartupConfigurationValidator.

Validate on application start:

Required configs:

Jwt__Key
ConnectionStrings__Default
AIService__BaseUrl
AIService__TimeoutSeconds

If missing:
- log critical error
- stop application startup.

----------------------------------------------------
2. DATABASE READINESS CHECK
----------------------------------------------------

Add hosted service:

DatabaseStartupCheck

Behavior:

- attempt DB connection
- run lightweight query
- retry with exponential backoff (max 5)
- fail application if unreachable.

----------------------------------------------------
3. AI SERVICE READINESS CHECK
----------------------------------------------------

Add AIStartupCheck hosted service.

Behavior:

- call AI health endpoint
- retry 3 times
- log degraded state if unavailable
- DO NOT crash API if AI unavailable.

----------------------------------------------------
4. GLOBAL EXCEPTION SAFETY
----------------------------------------------------

Ensure GlobalExceptionMiddleware:

- never exposes stack traces
- returns correlationId in error response
- structured logging for all unhandled exceptions.

----------------------------------------------------
5. REQUEST LOGGING STANDARDIZATION
----------------------------------------------------

Add RequestLoggingMiddleware.

Log:

- method
- path
- status code
- elapsed ms
- correlation id

Exclude:

/health/*
/metrics

----------------------------------------------------
6. SECURITY FINALIZATION
----------------------------------------------------

Add:

HSTS enabled in Production.
CookiePolicy enforced.
Disable server header exposure.

Ensure HTTPS redirection active.

----------------------------------------------------
7. FILE UPLOAD PROTECTION
----------------------------------------------------

Add safeguards:

- max request body size configurable
- reject double extensions
- sanitize filenames
- prevent path traversal.

----------------------------------------------------
8. BACKGROUND WORKER WATCHDOG
----------------------------------------------------

Create WorkerHeartbeatService.

Behavior:

- update heartbeat timestamp every 30s
- expose worker health status.

Extend readiness check to include worker heartbeat freshness.

----------------------------------------------------
9. HEALTH ENDPOINT STANDARDIZATION
----------------------------------------------------

Ensure endpoints:

GET /health/live
GET /health/ready
GET /health/startup

Startup:
- config valid
- DB reachable

Ready:
- DB reachable
- worker alive
- AI reachable or degraded.

----------------------------------------------------
10. METRICS COMPLETENESS
----------------------------------------------------

Add counters:

http_requests_total
http_request_duration_ms
background_jobs_total
background_job_failures_total

Expose via /metrics.

----------------------------------------------------
11. LOG CORRELATION GUARANTEE
----------------------------------------------------

Ensure CorrelationId exists for:

- every HTTP request
- every background job
- every AI request

Auto-generate if missing.

----------------------------------------------------
12. SAFE SHUTDOWN SUPPORT
----------------------------------------------------

Graceful shutdown:

- stop accepting requests
- allow background jobs to finish
- flush logs
- close DB connections cleanly.

----------------------------------------------------
13. ENVIRONMENT SAFETY
----------------------------------------------------

Production mode must:

- disable Swagger UI
- disable detailed errors
- enable optimized logging level.

----------------------------------------------------
14. VERSION ENDPOINT
----------------------------------------------------

Add endpoint:

GET /version

Returns:

- service name
- version
- build timestamp
- environment.

Values read from configuration.

----------------------------------------------------
15. FINAL VALIDATION PIPELINE
----------------------------------------------------

Create LaunchValidationHostedService.

Runs all validations at startup and logs:

"STUDYPILOT READY FOR LAUNCH"

Only after all checks succeed.

====================================================
OUTPUT RULE
====================================================

Generate ONLY:

- hosted services
- middleware
- validators
- endpoint controllers
- DI registrations
- configuration updates

NO README.
STOP AFTER IMPLEMENTATION.