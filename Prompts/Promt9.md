You are building STEP 8 — StudyPilot Frontend (Production MVP).

This is NOT a demo Angular app.
This MUST be a scalable, production-ready architecture aligned with an existing Clean Architecture backend.

Generate ONLY source code.
DO NOT generate README files, explanations, or long text outputs.
Minimize tokens. Produce code only.

====================================================
GLOBAL STACK (MANDATORY)
====================================================

Frontend Framework:
- Angular (LATEST LTS)
- Standalone components ONLY
- TypeScript strict mode ON
- RxJS latest compatible
- Signals allowed where appropriate

UI:
- Angular CDK
- TailwindCSS
- No heavy UI frameworks (NO Material unless required internally)

Architecture Style:
- Feature-first modular architecture
- Clean separation:
  core / shared / features

State:
- Service + RxJS state (NO NgRx for MVP)
- Must allow future NgRx migration

Networking:
- HttpClient
- Interceptor-based API pipeline

Auth:
- JWT Bearer (already implemented backend)

Build Target:
- Production-ready structure
- Lazy-loaded routes
- Environment-based configuration

====================================================
BACKEND CONTRACT (ALREADY EXISTS)
====================================================

Base API URL via environment config.

Endpoints:

POST /auth/register
POST /auth/login

POST /documents/upload
GET  /documents

POST /quiz/start
POST /quiz/submit

GET /progress/weak-topics

GET /health/ai

All responses wrapped in:

{
  success: boolean,
  data: T,
  errors?: string[]
}

JWT returned on login/register.

Header required:
Authorization: Bearer <token>

Correlation header automatically handled by backend.

====================================================
PROJECT STRUCTURE (STRICT)
====================================================

src/app/

core/
  auth/
  http/
  guards/
  interceptors/
  config/
  services/

shared/
  components/
  ui/
  models/
  utils/

features/
  auth/
  dashboard/
  documents/
  quiz/
  progress/

app.routes.ts
app.config.ts
main.ts

====================================================
CORE LAYER REQUIREMENTS
====================================================

1. AuthService
- login()
- register()
- logout()
- token storage
- expose currentUser$ observable

2. HTTP INTERCEPTORS

AuthInterceptor:
- attach JWT automatically

ApiResponseInterceptor:
- unwrap ApiResponse<T>
- throw typed errors if success=false

LoadingInterceptor:
- global request tracking (signal or subject)

3. API CLIENT

Create typed StudyPilotApiService with methods:

login()
register()
uploadDocument()
getDocuments()
startQuiz()
submitQuiz()
getWeakTopics()
getAIHealth()

NO raw HttpClient usage in feature components.

====================================================
FEATURE MODULES
====================================================

AUTH FEATURE
-------------
Pages:
- login
- register

Reactive forms
Validation matching backend rules.

Redirect after login.

DOCUMENTS FEATURE
-----------------
Pages:
- document-list
- upload-document

Upload:
- PDF only
- show "Processing" state
- optimistic UI update

Polling service:
poll document status every 5s (MVP).

QUIZ FEATURE
------------
Pages:
- quiz-player

Behavior:
- start quiz
- display MCQ questions
- submit answers
- show result summary

Local state only.

PROGRESS FEATURE
----------------
Page:
- weak-topics-dashboard

Display:
concept name
mastery score
simple progress visualization.

DASHBOARD FEATURE
-----------------
Landing page after login:
- upload shortcut
- weak topics preview
- AI health indicator.

====================================================
ROUTING
====================================================

Lazy load ALL feature routes.

Routes:

/auth/login
/auth/register
/dashboard
/documents
/quiz/:id
/progress

AuthGuard protects all except auth routes.

====================================================
ENVIRONMENT CONFIG
====================================================

environment.ts:

export const environment = {
  apiBaseUrl: ''
};

NO hardcoded URLs.

====================================================
ERROR HANDLING
====================================================

Global error handler service.

Handle:
401 → logout + redirect login
500 → toast notification

====================================================
PERFORMANCE RULES
====================================================

- OnPush change detection everywhere.
- trackBy in lists.
- Signals or BehaviorSubjects only.
- No component business logic duplication.

====================================================
TOKEN OPTIMIZATION RULE
====================================================

DO NOT generate:

- README
- comments explaining architecture
- placeholder tutorial text
- demo data generators

Generate ONLY necessary production code.

====================================================
EXPECTED RESULT
====================================================

A runnable Angular application with:

✔ authentication flow
✔ document upload
✔ async processing UI
✔ quiz experience
✔ mastery dashboard
✔ scalable architecture

STOP AFTER CODE GENERATION.
DO NOT EXPLAIN ANYTHING.