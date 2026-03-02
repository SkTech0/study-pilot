You are implementing ENTERPRISE-LEVEL ERROR HANDLING
for the StudyPilot production system.

IMPORTANT:
Generate ONLY source code.
DO NOT generate README files.
DO NOT explain architecture.
DO NOT output tutorial text.
NO placeholder comments.
Production-quality implementation only.

====================================================
SYSTEM CONTEXT (ALREADY BUILT)
====================================================

Backend:
- .NET 10 LTS
- Clean Architecture
- CQRS (MediatR)
- FluentValidation
- GlobalExceptionMiddleware exists
- CorrelationId propagation exists

Frontend:
- Angular (latest LTS)
- Standalone architecture
- Http interceptors already exist
- ApiResponse<T> contract used

Current API response:

{
  success: boolean,
  data?: T,
  errors?: string[]
}

We will REPLACE generic error handling
with enterprise structured errors.

====================================================
GOAL
====================================================

Backend becomes SINGLE SOURCE OF TRUTH
for ALL validation and error messages.

Frontend ONLY renders backend errors.

NO duplicated validation rules in UI.

NO generic "Something went wrong".

====================================================
PART 1 — ENTERPRISE ERROR CONTRACT
====================================================

Create shared error model:

Application/Common/Errors/AppError.cs

Properties:

- Code (string)
- Message (string)
- Field (string?)
- Severity (enum: Validation, Business, System)
- CorrelationId (string?)

Create enum ErrorCodes:

AUTH_INVALID_CREDENTIALS
AUTH_USER_EXISTS
DOCUMENT_TOO_LARGE
DOCUMENT_INVALID_FORMAT
QUIZ_NOT_FOUND
QUIZ_ALREADY_COMPLETED
AI_SERVICE_UNAVAILABLE
RATE_LIMIT_EXCEEDED
VALIDATION_FAILED
UNEXPECTED_ERROR

NO magic strings allowed anywhere else.

====================================================
PART 2 — RESULT MODEL UPDATE
====================================================

Update Result<T>:

Result<T>
- IsSuccess
- Value
- IReadOnlyList<AppError> Errors

Factory methods:

Success()
ValidationFailure(errors)
Failure(errors)

Remove string-based errors.

====================================================
PART 3 — DOMAIN & APPLICATION VALIDATION
====================================================

FluentValidation rules MUST:

Return AppError objects
NOT plain messages.

Create helper:

ValidationErrorFactory.Create(code, message, field)

All validators updated to use codes.

Examples:

Email invalid →
Code: VALIDATION_EMAIL_INVALID
Field: email

Upload too large →
Code: DOCUMENT_TOO_LARGE

====================================================
PART 4 — EXCEPTION MAPPING (CRITICAL)
====================================================

Enhance GlobalExceptionMiddleware.

Map exceptions:

ValidationException →
400 + Validation severity

DomainException →
409 + Business severity

UnauthorizedAccessException →
401

HttpRequestException (AI) →
503 AI_SERVICE_UNAVAILABLE

Fallback →
500 UNEXPECTED_ERROR

Response format:

{
  success:false,
  errors:[AppError],
  correlationId:"..."
}

NEVER expose stack traces.

====================================================
PART 5 — CONTROLLER SIMPLIFICATION
====================================================

Controllers MUST:

return Result<T> only.

No try/catch blocks inside controllers.

Mapping handled globally.

====================================================
PART 6 — ANGULAR ERROR PIPELINE
====================================================

Create:

core/models/app-error.model.ts

interface AppError:
- code
- message
- field?
- severity
- correlationId?

----------------------------------------------------

Update ApiResponseInterceptor:

If success=false:

throw EnterpriseApiError containing:
- errors[]
- correlationId

----------------------------------------------------

Create GlobalErrorHandlerService.

Behavior:

Validation errors:
→ show inline form errors using field mapping.

Business errors:
→ toast notification with message.

System errors:
→ global error banner with correlationId.

NO hardcoded messages.

====================================================
PART 7 — FORM ERROR BINDING
====================================================

Create FormErrorMapper utility:

Maps backend errors to Angular form controls.

Example:

field="email"
→ form.controls.email.setErrors({server:true})

Must support multiple errors.

====================================================
PART 8 — HTTP INTERCEPTOR BEHAVIOR
====================================================

Auth errors (401):
→ logout + redirect login.

Rate limit (429):
→ show retry message with countdown.

503 AI unavailable:
→ show degraded service banner.

====================================================
PART 9 — LOGGING ENRICHMENT
====================================================

All error logs include:

- ErrorCode
- CorrelationId
- UserId (if available)

Structured logging only.

====================================================
PART 10 — VALIDATION OWNERSHIP RULE
====================================================

Frontend MUST NOT duplicate:

- password rules
- file size rules
- quiz constraints

Frontend performs ONLY:

required field checks for UX.

All real validation from backend.

====================================================
OUTPUT RULE
====================================================

Generate ONLY:

- AppError models
- Result<T> updates
- validators update
- middleware updates
- Angular models
- interceptors
- form mapper
- error handler service

NO README.
NO explanations.
STOP AFTER IMPLEMENTATION.