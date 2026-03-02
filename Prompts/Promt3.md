Proceed to STEP 3 — Application Layer.

You are implementing use-case orchestration using CQRS and MediatR.

====================================================
APPLICATION LAYER RULES (STRICT)
====================================================

- Application depends ONLY on Domain.
- NO EF Core usage.
- NO infrastructure implementations.
- Define interfaces only.
- Business orchestration lives here.
- Use CQRS pattern.
- Every feature = Command or Query.

====================================================
FOLDER STRUCTURE
====================================================

Create:

Application/
 ├── Abstractions/
 │     Persistence/
 │     AI/
 │     BackgroundJobs/
 ├── Common/
 │     Behaviors/
 │     Models/
 ├── Documents/
 ├── Quiz/
 ├── Progress/

====================================================
INTERFACES (IMPORTANT)
====================================================

Create repository abstractions:

IUserRepository
IDocumentRepository
IConceptRepository
IQuizRepository
IUserConceptProgressRepository

Methods async-only.

Create Unit Of Work:

IUnitOfWork
- Task SaveChangesAsync()

====================================================
EXTERNAL SERVICE ABSTRACTIONS
====================================================

IAIService
- ExtractConceptsAsync()
- GenerateQuizAsync()

IBackgroundJobQueue
- Enqueue(Func<Task> job)

====================================================
CQRS IMPLEMENTATION
====================================================

Create Commands + Handlers:

1. UploadDocumentCommand
Input:
- UserId
- FileName
- StoragePath

Behavior:
- create Document entity
- save via repository
- enqueue background AI processing

----------------------------------------------------

2. StartQuizCommand
Input:
- DocumentId
- UserId

Behavior:
- request quiz generation via AI service
- create Quiz aggregate

----------------------------------------------------

3. SubmitQuizCommand
Input:
- QuizId
- Answers

Behavior:
- evaluate answers
- update UserConceptProgress
- persist changes

====================================================
QUERIES
====================================================

GetWeakConceptsQuery
Input:
- UserId

Output:
- concepts where masteryScore < 40

====================================================
VALIDATION
====================================================

Use FluentValidation:

Create validators for each command.

====================================================
PIPELINE BEHAVIORS
====================================================

Add MediatR behaviors:

- ValidationBehavior
- LoggingBehavior (simple abstraction)

====================================================
RESPONSE MODEL
====================================================

Create Result<T> wrapper:

- Success
- Error
- ValidationErrors

====================================================
OUTPUT RULE
====================================================

Generate ONLY:

- interfaces
- commands
- queries
- handlers
- validators
- behaviors
- result wrapper

DO NOT implement infrastructure.
DO NOT create EF code.
DO NOT create controllers.

STOP after completion.