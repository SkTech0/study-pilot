namespace StudyPilot.Application.Abstractions.BackgroundJobs;

public interface IBackgroundJobQueue
{
    /// <summary>Enqueues a document processing job for at-least-once execution. Persisted when using DB-backed implementation.</summary>
    Task EnqueueDocumentProcessingAsync(Guid documentId, string? correlationId, CancellationToken cancellationToken = default);
}
