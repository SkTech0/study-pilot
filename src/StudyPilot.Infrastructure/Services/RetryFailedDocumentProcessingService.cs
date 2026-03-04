using StudyPilot.Application.Abstractions.BackgroundJobs;
using StudyPilot.Application.Abstractions.Persistence;
using StudyPilot.Infrastructure.Persistence.Repositories;

namespace StudyPilot.Infrastructure.Services;

/// <summary>Resets failed documents/jobs and also recovers documents stuck in Processing (e.g. job completed without updating doc).</summary>
public sealed class RetryFailedDocumentProcessingService : IRetryFailedDocumentProcessing
{
    /// <summary>Documents in Processing for longer than this are considered stuck and reset when user clicks retry.</summary>
    private static readonly TimeSpan StuckProcessingCutoff = TimeSpan.FromMinutes(5);

    private readonly IDocumentRepository _documentRepository;
    private readonly IBackgroundJobRepository _jobRepository;
    private readonly IBackgroundJobQueue _jobQueue;

    public RetryFailedDocumentProcessingService(
        IDocumentRepository documentRepository,
        IBackgroundJobRepository jobRepository,
        IBackgroundJobQueue jobQueue)
    {
        _documentRepository = documentRepository;
        _jobRepository = jobRepository;
        _jobQueue = jobQueue;
    }

    public async Task<(int DocumentsReset, int JobsReset)> RetryAsync(CancellationToken cancellationToken = default)
    {
        var documentsReset = await _documentRepository.ResetFailedDocumentsToPendingAsync(cancellationToken);
        var jobsReset = await _jobRepository.ResetFailedJobsToPendingAsync(cancellationToken);

        // Recover documents stuck in Processing (e.g. job ran but couldn't claim or crashed before updating)
        var stuckCutoff = DateTime.UtcNow.Add(-StuckProcessingCutoff);
        var stuckIds = await _documentRepository.GetStuckProcessingDocumentIdsAsync(stuckCutoff, cancellationToken);
        if (stuckIds.Count > 0)
        {
            _ = await _jobRepository.ReleaseProcessingJobsForDocumentsAsync(stuckIds, cancellationToken);
            foreach (var docId in stuckIds)
            {
                await _documentRepository.ResetToPendingAsync(docId, cancellationToken);
                await _jobQueue.EnqueueDocumentProcessingAsync(docId, null, cancellationToken);
            }
            documentsReset += stuckIds.Count;
        }

        return (documentsReset, jobsReset);
    }
}
