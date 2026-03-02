namespace StudyPilot.API.Contracts.Responses;

public sealed record ChatMessageItemResponse(Guid MessageId, string Role, string Content, DateTime CreatedAtUtc, IReadOnlyList<Guid> CitedChunkIds);
