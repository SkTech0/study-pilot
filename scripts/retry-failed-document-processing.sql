-- Reset failed documents and failed background jobs to Pending so the worker can process them.
-- Run when you have items stuck in Failed state and want to reprocess them.
-- Usage: psql -U studypilot -d StudyPilot -f scripts/retry-failed-document-processing.sql

BEGIN;

-- Reset documents in Failed state to Pending (clear failure reason)
UPDATE "Documents"
SET "ProcessingStatus" = 'Pending', "FailureReason" = NULL
WHERE "ProcessingStatus" = 'Failed';

-- Reset background jobs in Failed state to Pending (clear retry count and claim)
UPDATE "BackgroundJobs"
SET "Status" = 'Pending',
    "RetryCount" = 0,
    "NextRetryAtUtc" = NULL,
    "ClaimedAtUtc" = NULL,
    "ClaimedBy" = NULL,
    "ErrorMessage" = NULL
WHERE "Status" = 'Failed';

COMMIT;
