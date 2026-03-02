namespace StudyPilot.Application.Chat.SendChatMessage;

public sealed record SendChatMessageResult(
    Guid AssistantMessageId,
    string Answer,
    IReadOnlyList<Guid> CitedChunkIds);

