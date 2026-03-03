using StudyPilot.Application.Abstractions.Persistence;
using StudyPilot.Infrastructure.Persistence.Repositories;

namespace StudyPilot.Infrastructure.Services;

public sealed class RetryFailedDocumentProcessingService : IRetryFailedDocumentProcessing
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IBackgroundJobRepository _jobRepository;

    public RetryFailedDocumentProcessingService(IDocumentRepository documentRepository, IBackgroundJobRepository jobRepository)
    {
        _documentRepository = documentRepository;
        _jobRepository = jobRepository;
    }

    public async Task<(int DocumentsReset, int JobsReset)> RetryAsync(CancellationToken cancellationToken = default)
    {
        var documentsReset = await _documentRepository.ResetFailedDocumentsToPendingAsync(cancellationToken);
        var jobsReset = await _jobRepository.ResetFailedJobsToPendingAsync(cancellationToken);
        return (documentsReset, jobsReset);
    }
}
