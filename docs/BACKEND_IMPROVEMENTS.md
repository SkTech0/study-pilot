# Backend & AI Service Improvements (StudyPilot)

## Summary

This document summarizes the backend and API improvements implemented on the `feature/backend-improvements` branch, plus recommended next steps from a senior backend / AI engineer review.

---

## Implemented in This Branch

### 1. **Solution file**
- **StudyPilot.sln** (classic format) added at repo root for IDE and CI (e.g. `dotnet build StudyPilot.sln`, `dotnet test StudyPilot.sln`).
- **StudyPilot.slnx** (new format) was already present; both are valid.

### 2. **Centralized current user resolution**
- **ClaimsPrincipalExtensions.GetCurrentUserId()** – single place to read user ID from `NameIdentifier` or `sub` claims.
- **ControllerBaseExtensions.UnauthorizedIfNoUser&lt;T&gt;()** – returns 401 `ApiResponse` when user cannot be resolved; avoids duplicated claim parsing and error building in every controller.
- **ProgressController**, **DocumentsController**, and **QuizController** updated to use these extensions instead of repeating the same logic.

### 3. **Request logging (correlation and request name)**
- **LoggingBehavior** (MediatR pipeline) now accepts optional **ICorrelationIdAccessor** and logs **RequestName** and **CorrelationId** on “Handling” and “Handled”, improving traceability in logs and APM.

### 4. **AIService configuration**
- **AIService:TimeoutSeconds** is no longer required at startup; **StartupConfigurationValidator** no longer fails when it is missing.
- **AIServiceOptions** already had default `TimeoutSeconds = 60`; **DependencyInjection** now uses a fallback (60s) when the value is missing or ≤ 0 so the HTTP client always has a valid timeout.

### 5. **Unit tests**
- **GetWeakConceptsQueryValidatorTests** – validation for empty vs valid `UserId`.
- **GetWeakConceptsQueryHandlerTests** – cached result, empty weak progress, and full flow (progress + concepts) with Moq.
- Test project references **Moq**; all tests run with `dotnet test StudyPilot.sln`.

---

## Recommended Next Steps (not in this branch)

- **Health checks abstraction**  
  HealthController currently depends on **StudyPilotDbContext** and **IStudyPilotAIClient**. Consider ASP.NET Core `IHealthCheck` implementations (e.g. `AddCheck<DatabaseHealthCheck>()`, `AddCheck<AIHealthCheck>()`) and `MapHealthChecks()` so the API does not depend on concrete infra types for health.

- **Background job durability**  
  In-memory queue is fine for single instance; for multi-instance or durability, introduce a distributed queue (e.g. Redis, RabbitMQ, or cloud queue) and a retry/dead-letter strategy for failed document processing.

- **Document processing retry**  
  When the background job fails, the document is marked Failed with no retry. Add a retry policy and/or an endpoint to re-trigger processing for a document.

- **More test coverage**  
  Add unit tests for other command/query handlers and validators; add integration tests for API and DB (e.g. Auth, Documents, Quiz).

- **OpenAPI in production**  
  OpenAPI is only mapped in Development. If you need stable API docs in production, expose a dedicated docs endpoint (e.g. with auth or read-only key).

---

## How to run

```bash
git checkout feature/backend-improvements
dotnet build StudyPilot.sln
dotnet test StudyPilot.sln
# Run API (from repo root)
cd src/StudyPilot.API && dotnet run
```

Health: `GET /health/live`, `GET /health/ready`, `GET /health/ai`.  
Version: `GET /version`.
