namespace StudyPilot.API.Contracts.Responses;

public sealed record SendChatMessageResponse(
    Guid AssistantMessageId,
    string Answer,
    IReadOnlyList<Guid> CitedChunkIds,
    string Status = "ok",
    int? ChunkCount = null,
    double? BestScore = null,
    string? Reason = null);
