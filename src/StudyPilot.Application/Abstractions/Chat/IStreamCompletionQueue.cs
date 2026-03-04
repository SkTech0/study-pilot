using StudyPilot.Application.Chat;

namespace StudyPilot.Application.Abstractions.Chat;

/// <summary>
/// Enqueues stream completion work so it runs inside a dedicated scope (no request-scoped services).
/// Worker creates scope via IServiceScopeFactory and runs stream + persist.
/// </summary>
public interface IStreamCompletionQueue
{
    Task EnqueueAsync(
        StreamCompletionWorkItem work,
        IStreamTokenWriter writer,
        TaskCompletionSource streamComplete,
        TaskCompletionSource<ChatStatus> statusTcs,
        CancellationToken cancellationToken = default);
}
