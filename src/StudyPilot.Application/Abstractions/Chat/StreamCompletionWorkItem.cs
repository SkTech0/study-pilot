using StudyPilot.Application.Knowledge.Models;

namespace StudyPilot.Application.Abstractions.Chat;

/// <summary>
/// Data required for a background worker to run stream chat and persist the result. No scoped services.
/// </summary>
public sealed record StreamCompletionWorkItem(
    ChatRequest Request,
    IReadOnlyList<RetrievedChunk> Retrieved);
