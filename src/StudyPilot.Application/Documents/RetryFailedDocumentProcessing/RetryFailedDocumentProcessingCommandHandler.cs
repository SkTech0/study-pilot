using MediatR;
using StudyPilot.Application.Abstractions.Persistence;
using StudyPilot.Application.Common.Models;

namespace StudyPilot.Application.Documents.RetryFailedDocumentProcessing;

public sealed class RetryFailedDocumentProcessingCommandHandler : IRequestHandler<RetryFailedDocumentProcessingCommand, Result<RetryFailedDocumentProcessingResult>>
{
    private readonly IRetryFailedDocumentProcessing _retryService;

    public RetryFailedDocumentProcessingCommandHandler(IRetryFailedDocumentProcessing retryService)
    {
        _retryService = retryService;
    }

    public async Task<Result<RetryFailedDocumentProcessingResult>> Handle(RetryFailedDocumentProcessingCommand request, CancellationToken cancellationToken)
    {
        var (documentsReset, jobsReset) = await _retryService.RetryAsync(cancellationToken);
        return Result<RetryFailedDocumentProcessingResult>.Success(new RetryFailedDocumentProcessingResult(documentsReset, jobsReset));
    }
}
