Proceed to STEP 4 — Infrastructure Layer.

You are implementing infrastructure adapters for Application abstractions.

====================================================
INFRASTRUCTURE RULES (STRICT)
====================================================

- Infrastructure depends on Application and Domain.
- NEVER reverse dependency.
- Implement interfaces defined in Application.
- All persistence logic lives here.
- Domain must remain unaware of EF Core.

====================================================
TECH STACK
====================================================

- .NET 10
- EF Core (latest stable compatible)
- PostgreSQL provider
- pgvector support
- Serilog logging integration

====================================================
FOLDER STRUCTURE
====================================================

Infrastructure/
 ├── Persistence/
 │     DbContext/
 │     Configurations/
 │     Repositories/
 ├── AI/
 ├── BackgroundJobs/
 ├── Logging/

====================================================
DB CONTEXT
====================================================

Create StudyPilotDbContext.

Requirements:

- DbSet for all entities
- ApplyConfigurationsFromAssembly
- UTC DateTime enforcement

Override SaveChangesAsync:

- update UpdatedAtUtc automatically

====================================================
ENTITY CONFIGURATIONS
====================================================

Use Fluent API ONLY.

Create configuration class per entity:

UserConfiguration
DocumentConfiguration
ConceptConfiguration
QuizConfiguration
QuestionConfiguration
UserAnswerConfiguration
UserConceptProgressConfiguration

Rules:

- UUID primary keys
- relationships defined explicitly
- indexes:
    UserId
    DocumentId
    ConceptId

NO data annotations.

====================================================
REPOSITORY IMPLEMENTATIONS
====================================================

Implement all repository interfaces.

Repositories must:

- be async
- use cancellation tokens
- not expose IQueryable externally
- return domain entities only

====================================================
UNIT OF WORK
====================================================

Implement IUnitOfWork using DbContext.

SaveChangesAsync delegates to DbContext.

====================================================
AI SERVICE IMPLEMENTATION
====================================================

Create placeholder OpenAIService implementing IAIService.

Implementation:

- HttpClientFactory usage
- async calls
- no hardcoded secrets
- configuration via options pattern

Return mock data for now but structure must be production-ready.

====================================================
BACKGROUND JOB QUEUE
====================================================

Implement InMemoryBackgroundJobQueue:

- Channel-based queue
- Background worker service ready

Create DocumentProcessingJobFactory implementation.

====================================================
LOGGING
====================================================

Implement IRequestLogger using Serilog abstraction.

====================================================
DEPENDENCY REGISTRATION
====================================================

Create extension:

AddInfrastructure(IServiceCollection services, IConfiguration config)

Register:

- DbContext
- repositories
- unit of work
- AI service
- background queue
- logging adapter

====================================================
OUTPUT RULE
====================================================

Generate ONLY infrastructure implementations.

DO NOT create controllers.
DO NOT modify Application layer.

STOP after completion.