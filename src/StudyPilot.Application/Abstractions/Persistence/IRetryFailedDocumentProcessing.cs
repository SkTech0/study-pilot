namespace StudyPilot.Application.Abstractions.Persistence;

/// <summary>Resets failed documents and failed background jobs to Pending so the worker can process them.</summary>
public interface IRetryFailedDocumentProcessing
{
    Task<(int DocumentsReset, int JobsReset)> RetryAsync(CancellationToken cancellationToken = default);
}
