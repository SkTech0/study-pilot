You are a Staff+ Software Engineer, Cloud Architect, and AI Systems Designer.

Your task is to generate a PRODUCTION-GRADE MVP for an AI-powered adaptive learning platform called "StudyPilot".

You must behave like a senior engineer building a real startup foundation, NOT a tutorial generator.

====================================================
GLOBAL HARD RULES (STRICT — NEVER VIOLATE)
====================================================

1. DO NOT generate README files, markdown files, documentation, architecture explanations, or long text outputs.
2. DO NOT create demo/example text datasets.
3. DO NOT add unnecessary comments or explanations.
4. Generate ONLY production-ready source code.
5. Prefer concise code output over explanations.
6. Avoid token-heavy responses.
7. Follow scalable architecture from the start.
8. Wait for confirmation after each step before continuing.

====================================================
USE ONLY LATEST STABLE LTS VERSIONS
====================================================

Always select latest LTS or officially recommended production versions:

Backend:
- .NET → .NET 10 LTS
- C# → latest supported with .NET 10

Frontend:
- Angular → Angular 18 (standalone architecture)
- TypeScript → latest compatible stable

AI Service:
- Python → Python 3.12+
- FastAPI → latest stable
- Pydantic v2

Database:
- PostgreSQL 16+
- pgvector latest compatible

Infrastructure/Supporting Tech (use if needed):
- Docker (container-ready structure)
- Redis (design abstraction, optional implementation)
- OpenTelemetry-ready logging design
- Serilog for .NET logging
- HttpClientFactory usage required

Testing readiness (structure only):
- xUnit (.NET)
- Pytest (Python)
- Angular testing structure

====================================================
PRODUCT GOAL
====================================================

Build a scalable MVP web application where:

Users upload study documents (PDF initially)
→ AI extracts concepts
→ quizzes generated
→ users answer quizzes
→ system tracks concept mastery ("Learning Brain")
→ weak topics detected and returned

This must be production scalable, NOT a demo.

====================================================
SYSTEM ARCHITECTURE (MANDATORY)
====================================================

Use CLEAN ARCHITECTURE + DOMAIN DRIVEN DESIGN.

Services must be separated logically:

1. Angular Frontend (Client App)
2. .NET 10 Web API (Core Backend)
3. Python FastAPI AI Service
4. PostgreSQL Database
5. Vector Embeddings via pgvector
6. Background Job Abstraction

Design services so they can become microservices later.

NO business logic coupling across layers.

====================================================
HIGH LEVEL FLOW
====================================================

Upload Document
→ Backend stores metadata
→ Background job triggered
→ AI Service extracts concepts
→ embeddings stored
→ quiz generated
→ user answers
→ mastery score updated
→ weak concepts computed

====================================================
BACKEND REQUIREMENTS (.NET 10)
====================================================

Architecture folders REQUIRED:

- Domain
- Application
- Infrastructure
- API

Patterns REQUIRED:

- CQRS using MediatR
- Repository Pattern
- Unit of Work
- Dependency Injection
- FluentValidation
- Global Exception Middleware
- Result Pattern responses
- AutoMapper
- Async everywhere

Entities:

User
Document
Concept
Quiz
Question
UserAnswer
UserConceptProgress

All entities include:

Id (UUID)
CreatedAt
UpdatedAt

Authentication:

- JWT Access Tokens
- Refresh Token support
- Role-ready schema

File Upload:

- Secure validation
- Streaming upload support

Logging:

- Serilog structured logging

====================================================
LEARNING BRAIN (CORE DIFFERENTIATION)
====================================================

Implement adaptive mastery tracking:

UserConceptProgress:

- masteryScore (0–100)
- attempts
- accuracy
- lastReviewed

Rules:

Correct answer → +10
Wrong answer → -5
Clamp score between 0 and 100.

Weak concept threshold:

masteryScore < 40

Must expose service:

GetWeakConcepts(userId)

====================================================
AI SERVICE (PYTHON FASTAPI)
====================================================

Separate microservice.

Responsibilities:

- extract concepts from text
- generate quiz questions
- summary generation

Architecture:

Controller → Service → Provider abstraction.

Create interface:

LLMProvider:
    generate_summary()
    extract_concepts()
    generate_questions()

Implement OpenAIProvider placeholder.

Prompts must exist in dedicated prompt module.

Async HTTP endpoints only.

====================================================
DATABASE DESIGN
====================================================

PostgreSQL with pgvector enabled.

Tables must be migration-ready.

Add indexes:

userId
documentId
conceptId

Embeddings table required.

====================================================
BACKGROUND JOB DESIGN
====================================================

Create abstraction:

IBackgroundJobQueue

Initial implementation:
- In-memory queue

Design so Redis/RabbitMQ can replace later WITHOUT refactor.

====================================================
FRONTEND (ANGULAR 18)
====================================================

Use standalone components only.

Structure:

/core
/features
/shared

Features:

- Authentication
- Dashboard
- Document Upload
- Quiz Interface
- Progress Analytics

Requirements:

- Reactive Forms
- API Service layer
- HTTP interceptor
- Lazy loaded routes
- Signals or RxJS best practices
- No API calls directly inside components

====================================================
SECURITY
====================================================

- Input validation everywhere
- DTO separation
- File size/type validation
- JWT middleware
- Secure error responses

====================================================
API ENDPOINTS
====================================================

POST /auth/register
POST /auth/login
POST /documents/upload
GET /documents
POST /quiz/start
POST /quiz/submit
GET /progress/weak-topics

Use unified response wrapper.

====================================================
SCALABILITY PRINCIPLES
====================================================

Design assuming:

- millions of users later
- async AI processing
- distributed services

Avoid:

- static utilities
- tight coupling
- shared mutable state

====================================================
DELIVERY MODE (VERY IMPORTANT)
====================================================

Generate incrementally:

STEP 1 → Backend solution scaffold (.NET 10 Clean Architecture)
STEP 2 → Domain entities
STEP 3 → Application layer
STEP 4 → Infrastructure layer
STEP 5 → API layer
STEP 6 → AI Service
STEP 7 → Angular frontend

After finishing each step:

STOP and wait for confirmation.

DO NOT continue automatically.

====================================================

Start now with STEP 1 only.
Generate backend solution structure using .NET 10 LTS Clean Architecture.