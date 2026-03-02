Proceed to STEP 2 — Domain Modeling.

Follow existing domain purity rules.

====================================================
DOMAIN HARD CONSTRAINTS
====================================================

- Domain must remain framework-independent.
- NO NuGet dependencies.
- NO EF Core attributes.
- NO persistence annotations.
- Only pure C# + BCL allowed.
- All timestamps MUST use UTC (DateTime.UtcNow).
- Domain models must be rich (behavior included).

====================================================
GOAL
====================================================

Implement core business domain for adaptive learning system.

Focus on modeling learning intelligence and mastery tracking.

====================================================
BASE TYPES
====================================================

Create BaseEntity:

Properties:
- Guid Id
- DateTime CreatedAtUtc
- DateTime UpdatedAtUtc

Behavior:
- Set timestamps automatically using DateTime.UtcNow
- Protected method Touch() updates UpdatedAtUtc

====================================================
VALUE OBJECTS
====================================================

Create Value Objects:

1. Email
- validates format
- immutable
- equality based on value

2. MasteryScore
- range 0–100 enforced
- Increase(int value)
- Decrease(int value)
- Clamp automatically

====================================================
ENUMS
====================================================

ProcessingStatus:
Pending
Processing
Completed
Failed

QuestionType:
MCQ
ShortAnswer

UserRole:
Student
Admin

====================================================
ENTITIES
====================================================

User
- Email
- PasswordHash
- Role
- IReadOnlyCollection<Document>

Document
- UserId
- FileName
- StoragePath
- ProcessingStatus
- IReadOnlyCollection<Concept>

Concept
- DocumentId
- Name
- Description (optional)

Quiz
- DocumentId
- CreatedForUserId
- IReadOnlyCollection<Question>

Question
- QuizId
- Text
- QuestionType
- CorrectAnswer
- IReadOnlyCollection<string> Options

UserAnswer
- UserId
- QuestionId
- SubmittedAnswer
- IsCorrect

UserConceptProgress (CORE AGGREGATE)

Fields:
- UserId
- ConceptId
- MasteryScore
- Attempts
- Accuracy
- LastReviewedUtc

Domain Methods:

RecordCorrectAnswer()
RecordWrongAnswer()

Rules:
Correct → +10 mastery
Wrong → -5 mastery
Score clamped 0–100
Attempts incremented automatically
Accuracy recalculated internally

====================================================
MODELING RULES
====================================================

- Private setters wherever possible.
- Collections exposed as IReadOnlyCollection.
- Use constructors or factory methods.
- Prevent invalid states.
- Encapsulate behavior inside entities.

====================================================
OUTPUT RULE
====================================================

Generate ONLY domain entities, value objects, enums, and base classes.

DO NOT create:
- repositories
- DTOs
- EF configurations
- services
- handlers

STOP after completion and wait.