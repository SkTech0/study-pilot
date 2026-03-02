Proceed to STEP 5 — API Layer.

You are implementing HTTP adapters for Application use cases.

====================================================
API LAYER RULES (STRICT)
====================================================

- Controllers must remain thin.
- NO business logic in controllers.
- Controllers only call MediatR.
- Application layer handles behavior.
- Use DTOs for requests/responses.
- Never expose domain entities directly.

====================================================
STRUCTURE
====================================================

API/
 ├── Controllers/
 ├── Contracts/
 │     Requests/
 │     Responses/
 ├── Middleware/
 ├── Extensions/

====================================================
GLOBAL MIDDLEWARE
====================================================

Create:

1. GlobalExceptionMiddleware
   - converts exceptions into standardized responses

2. ValidationException handling → 400 response

====================================================
RESPONSE FORMAT
====================================================

Create ApiResponse<T>:

- success
- data
- errors

All endpoints return ApiResponse.

====================================================
AUTHENTICATION
====================================================

Implement JWT authentication setup:

- AddJwtAuthentication extension
- Token validation parameters
- Placeholder token generation service (no external identity yet)

Endpoints:

POST /auth/register
POST /auth/login

Use MediatR commands (create minimal placeholders if missing).

====================================================
DOCUMENT CONTROLLER
====================================================

POST /documents/upload

Requirements:

- Accept IFormFile
- validate file type/size
- save file to local storage abstraction
- send UploadDocumentCommand

Return documentId.

GET /documents
(simple placeholder query)

====================================================
QUIZ CONTROLLER
====================================================

POST /quiz/start
→ StartQuizCommand

POST /quiz/submit
→ SubmitQuizCommand

====================================================
PROGRESS CONTROLLER
====================================================

GET /progress/weak-topics
→ GetWeakConceptsQuery

====================================================
MAPPING
====================================================

Use AutoMapper profiles for:

Request DTO → Command
Result → Response DTO

====================================================
DEPENDENCY SETUP
====================================================

Create extension:

AddApiLayer(IServiceCollection services)

Register:

- MediatR
- AutoMapper
- Validators
- Middleware

====================================================
PROGRAM CONFIGURATION
====================================================

Update Program.cs:

- AddInfrastructure()
- AddApiLayer()
- JWT auth
- Serilog request logging
- Middleware pipeline
- MapControllers()

====================================================
OUTPUT RULE
====================================================

Generate ONLY API layer code.

DO NOT modify Domain or Application logic.
DO NOT generate documentation.

STOP after completion.