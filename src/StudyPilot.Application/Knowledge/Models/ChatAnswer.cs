namespace StudyPilot.Application.Knowledge.Models;

public sealed record ChatAnswer(
    string Answer,
    IReadOnlyList<Guid> CitedChunkIds,
    string? ModelUsed = null);

