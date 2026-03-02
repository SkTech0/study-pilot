namespace StudyPilot.API.Contracts.Responses;

public sealed record DocumentResponse(Guid Id, string FileName, string Status, DateTime CreatedAt, string? FailureReason = null);
