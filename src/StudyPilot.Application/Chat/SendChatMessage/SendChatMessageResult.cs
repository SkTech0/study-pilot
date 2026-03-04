using StudyPilot.Application.Chat;

namespace StudyPilot.Application.Chat.SendChatMessage;

public sealed record SendChatMessageResult(
    Guid AssistantMessageId,
    string Answer,
    IReadOnlyList<Guid> CitedChunkIds,
    ChatStatus Status = ChatStatus.Ok,
    int? ChunkCount = null,
    double? BestScore = null,
    string? Reason = null);

