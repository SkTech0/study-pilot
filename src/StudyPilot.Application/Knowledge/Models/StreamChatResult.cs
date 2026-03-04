using StudyPilot.Application.Chat;

namespace StudyPilot.Application.Knowledge.Models;

public sealed record StreamChatResult(
    IReadOnlyList<Guid> CitedChunkIds,
    string? ModelUsed,
    ChatStatus Status = ChatStatus.Ok,
    bool FallbackUsed = false);
