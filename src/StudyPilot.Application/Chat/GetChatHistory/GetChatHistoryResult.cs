using StudyPilot.Domain.Enums;

namespace StudyPilot.Application.Chat.GetChatHistory;

public sealed record GetChatHistoryResult(
    Guid SessionId,
    int TotalCount,
    int PageNumber,
    int PageSize,
    IReadOnlyList<ChatMessageItem> Messages);

public sealed record ChatMessageItem(
    Guid MessageId,
    ChatRole Role,
    string Content,
    DateTime CreatedAtUtc,
    IReadOnlyList<Guid> CitedChunkIds);

