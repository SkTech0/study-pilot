-- Creates Phase 3 learning intelligence tables if missing.
-- Run with: psql -U your_user -d your_database -f scripts/create-learning-intelligence-tables.sql
-- Or apply all pending migrations: dotnet ef database update --project src/StudyPilot.Infrastructure --startup-project src/StudyPilot.API

-- UserConceptMasteries (required for mastery, tutor, suggestions)
CREATE TABLE IF NOT EXISTS "UserConceptMasteries" (
    "Id" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "ConceptId" uuid NOT NULL,
    "MasteryScore" integer NOT NULL,
    "ConfidenceScore" double precision NOT NULL,
    "Attempts" integer NOT NULL,
    "CorrectAnswers" integer NOT NULL,
    "LastInteractionUtc" timestamp with time zone NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_UserConceptMasteries" PRIMARY KEY ("Id")
);
CREATE INDEX IF NOT EXISTS "IX_UserConceptMasteries_UserId" ON "UserConceptMasteries" ("UserId");
CREATE INDEX IF NOT EXISTS "IX_UserConceptMasteries_ConceptId" ON "UserConceptMasteries" ("ConceptId");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_UserConceptMasteries_UserId_ConceptId" ON "UserConceptMasteries" ("UserId", "ConceptId");
CREATE INDEX IF NOT EXISTS "IX_UserConceptMasteries_UserId_MasteryScore" ON "UserConceptMasteries" ("UserId", "MasteryScore");

-- LearningInsights
CREATE TABLE IF NOT EXISTS "LearningInsights" (
    "Id" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "ConceptId" uuid NOT NULL,
    "InsightType" character varying(32) NOT NULL,
    "Source" character varying(16) NOT NULL,
    "Confidence" double precision NOT NULL,
    "CreatedUtc" timestamp with time zone NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_LearningInsights" PRIMARY KEY ("Id")
);
CREATE INDEX IF NOT EXISTS "IX_LearningInsights_UserId" ON "LearningInsights" ("UserId");
CREATE INDEX IF NOT EXISTS "IX_LearningInsights_ConceptId" ON "LearningInsights" ("ConceptId");
CREATE INDEX IF NOT EXISTS "IX_LearningInsights_UserId_ConceptId" ON "LearningInsights" ("UserId", "ConceptId");

-- QuizConceptOrders
CREATE TABLE IF NOT EXISTS "QuizConceptOrders" (
    "QuizId" uuid NOT NULL,
    "QuestionIndex" integer NOT NULL,
    "ConceptId" uuid NOT NULL,
    CONSTRAINT "PK_QuizConceptOrders" PRIMARY KEY ("QuizId", "QuestionIndex")
);
CREATE INDEX IF NOT EXISTS "IX_QuizConceptOrders_QuizId" ON "QuizConceptOrders" ("QuizId");
