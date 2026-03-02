namespace StudyPilot.API.Contracts.Responses;

public sealed record UploadDocumentResponse(Guid DocumentId, string Status = "processing");
