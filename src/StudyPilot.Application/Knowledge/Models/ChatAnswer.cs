using StudyPilot.Application.Chat;

namespace StudyPilot.Application.Knowledge.Models;

public sealed record ChatAnswer(
    string Answer,
    IReadOnlyList<Guid> CitedChunkIds,
    string? ModelUsed = null,
    ChatStatus Status = ChatStatus.Ok,
    bool FallbackUsed = false);

