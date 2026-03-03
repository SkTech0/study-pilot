namespace StudyPilot.API.Contracts.Responses;

public sealed record RetryFailedDocumentProcessingResponse(int DocumentsReset, int JobsReset);
