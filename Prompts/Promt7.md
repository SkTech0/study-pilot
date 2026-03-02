You are building STEP 7 — Background Processing & AI Integration for StudyPilot.

This is a PRODUCTION MVP feature, not a demo.

====================================================
GLOBAL STACK (DO NOT CHANGE)
====================================================

Backend:
- .NET 10 LTS
- C# latest supported by .NET 10
- ASP.NET Core Web API

Database:
- PostgreSQL 16+
- EF Core (latest compatible)
- pgvector ready (do not implement usage yet)

AI Service:
- Python 3.12+
- FastAPI microservice (already exists)

Architecture:
- Clean Architecture (STRICT)
- CQRS via MediatR
- Repository pattern
- Background worker pattern

Logging:
- Serilog
- OpenTelemetry-ready structure

Networking:
- IHttpClientFactory REQUIRED

Testing structure only:
- xUnit project scaffold (no tests yet)

====================================================
PRODUCT GOAL
====================================================

Enable asynchronous AI-powered learning pipeline:

User uploads document
→ backend stores metadata
→ background job triggered
→ worker calls AI microservice
→ concepts extracted
→ database updated
→ mastery system enabled

API must NEVER wait for AI processing.

This is real product behavior.

====================================================
ARCHITECTURE FLOW
====================================================

Controller
   ↓
Application Command
   ↓
Background Queue
   ↓
Worker Service
   ↓
AI HTTP Client
   ↓
Python AI Service
   ↓
Database Update

====================================================
EXISTING SYSTEM (ALREADY BUILT)
====================================================

DO NOT REWRITE:

- Domain entities
- Application CQRS layer
- Infrastructure repositories
- InMemoryBackgroundJobQueue
- BackgroundJobWorker
- DocumentProcessingJobFactory
- JWT authentication
- Python AI service

Only extend integration.

====================================================
STEP 7 IMPLEMENTATION
====================================================

----------------------------------------------------
1. AI SERVICE HTTP CLIENT
----------------------------------------------------

Create:

Infrastructure/AI/IStudyPilotAIClient.cs
Infrastructure/AI/StudyPilotAIClient.cs

Interface methods:

Task<IReadOnlyList<ConceptDto>> ExtractConceptsAsync(
    Guid documentId,
    string text,
    CancellationToken ct);

Task<GenerateQuizResultDto> GenerateQuizAsync(
    Guid documentId,
    IReadOnlyList<string> concepts,
    int questionCount,
    CancellationToken ct);

Requirements:

- IHttpClientFactory usage
- Fully async
- CancellationToken mandatory
- System.Text.Json serialization
- No dynamic typing
- Typed DTOs only

Endpoints:

POST /extract-concepts
POST /generate-quiz

----------------------------------------------------
2. CONFIGURATION
----------------------------------------------------

Create options:

AIServiceOptions.cs

Properties:

- BaseUrl
- TimeoutSeconds

Bind from configuration:

"AIService": {
  "BaseUrl": "http://study-pilot-ai:8000",
  "TimeoutSeconds": 60
}

Use IOptions pattern.

----------------------------------------------------
3. RESILIENT HTTP PIPELINE
----------------------------------------------------

Register typed client:

services.AddHttpClient<IStudyPilotAIClient, StudyPilotAIClient>()
    .AddStandardResilienceHandler();

Do NOT manually configure Polly.

Timeout configured via options.

----------------------------------------------------
4. DOCUMENT PROCESSING PIPELINE
----------------------------------------------------

Update DocumentProcessingJobFactory.

Processing steps:

1. Load document from repository
2. Set ProcessingStatus = Processing
3. Save changes
4. Call AI ExtractConceptsAsync
5. Create Concept entities
6. Persist concepts
7. Set ProcessingStatus = Completed
8. Commit UnitOfWork

Failure flow:

- Catch ALL exceptions
- Set status = Failed
- Log structured error
- Worker must continue

No retries implemented yet.

----------------------------------------------------
5. DOCUMENT STATE CONTROL
----------------------------------------------------

Ensure lifecycle:

Uploaded → Processing → Completed | Failed

Only background worker controls transitions.

Controllers cannot modify status.

----------------------------------------------------
6. CONTROLLER BEHAVIOR (UPLOAD)
----------------------------------------------------

DocumentsController upload:

After persistence:

_backgroundQueue.Enqueue(
    jobFactory.CreateProcessDocumentJob(documentId));

Return immediately:

ApiResponse.Ok({
    documentId,
    status = "processing"
})

Never call AI directly.

----------------------------------------------------
7. BACKGROUND WORKER HARDENING
----------------------------------------------------

BackgroundJobWorker must:

- infinite processing loop
- await dequeue
- create IServiceScope per job
- support cancellation token
- catch all exceptions
- structured logging
- never terminate unexpectedly

----------------------------------------------------
8. AI HEALTH CHECK ENDPOINT
----------------------------------------------------

Create:

GET /health/ai

Behavior:

- Calls AI service GET /health
- Returns Healthy / Degraded

Controller must use AI client abstraction.

----------------------------------------------------
9. STRUCTURED LOGGING (SERILOG)
----------------------------------------------------

Add logs:

DocumentProcessingStarted
ConceptExtractionCompleted
DocumentProcessingCompleted
DocumentProcessingFailed

Include properties:

DocumentId
ElapsedMilliseconds

Prepare structure compatible with OpenTelemetry.

----------------------------------------------------
10. REDIS-READY DESIGN (ABSTRACTION ONLY)
----------------------------------------------------

Create interface:

IBackgroundQueueMetrics

Purpose:

Future Redis queue monitoring.

Provide InMemory implementation only.

Do NOT add Redis dependency.

----------------------------------------------------
11. TEST PROJECT STRUCTURE
----------------------------------------------------

Create project:

tests/StudyPilot.Application.Tests (xUnit)

Only scaffold:

- reference Application project
- empty test class

No tests required.

----------------------------------------------------
12. DOCKER READINESS (STRUCTURE ONLY)
----------------------------------------------------

Ensure:

- No localhost hardcoding
- Base URLs configurable
- Environment-variable friendly configuration

Do NOT create Dockerfiles yet.

====================================================
NON-GOALS
====================================================

DO NOT IMPLEMENT:

- Message brokers
- Redis implementation
- pgvector usage
- Caching
- LangChain
- Frontend changes
- Deployment scripts

====================================================
EXPECTED RESULT
====================================================

System becomes production-style async:

Upload → Queue → Worker → AI → Concepts saved.

API latency independent from AI latency.

====================================================
OUTPUT RULE
====================================================

Generate ONLY code:

- interfaces
- implementations
- updated factory
- controller updates
- options classes
- DI registration
- health endpoint
- test scaffold

NO README.
NO explanations.
STOP after implementation completes.  