using StudyPilot.Application.Abstractions.BackgroundJobs;
using StudyPilot.Application.Abstractions.Observability;
using StudyPilot.Application.Abstractions.Persistence;
using StudyPilot.Application.Abstractions.UsageGuard;
using StudyPilot.Application.Common.Errors;
using StudyPilot.Application.Common.Models;
using MediatR;
using StudyPilot.Domain.Entities;

namespace StudyPilot.Application.Documents.UploadDocument;

public sealed class UploadDocumentCommandHandler : IRequestHandler<UploadDocumentCommand, Result<UploadDocumentResult>>
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBackgroundJobQueue _jobQueue;
    private readonly IDocumentProcessingJobFactory _jobFactory;
    private readonly ICorrelationIdAccessor _correlationIdAccessor;
    private readonly IUsageGuardService _usageGuard;

    public UploadDocumentCommandHandler(
        IDocumentRepository documentRepository,
        IUnitOfWork unitOfWork,
        IBackgroundJobQueue jobQueue,
        IDocumentProcessingJobFactory jobFactory,
        ICorrelationIdAccessor correlationIdAccessor,
        IUsageGuardService usageGuard)
    {
        _documentRepository = documentRepository;
        _unitOfWork = unitOfWork;
        _jobQueue = jobQueue;
        _jobFactory = jobFactory;
        _correlationIdAccessor = correlationIdAccessor;
        _usageGuard = usageGuard;
    }

    public async Task<Result<UploadDocumentResult>> Handle(UploadDocumentCommand request, CancellationToken cancellationToken)
    {
        if (!await _usageGuard.CanUploadDocumentAsync(request.UserId, cancellationToken))
            return Result<UploadDocumentResult>.Failure(new AppError(ErrorCodes.DocumentUploadLimitReached, "Document upload limit reached for today.", null, ErrorSeverity.Business));
        var document = new Document(request.UserId, request.FileName, request.StoragePath);
        await _documentRepository.AddAsync(document, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var correlationId = _correlationIdAccessor.Get();
        await _jobQueue.EnqueueDocumentProcessingAsync(document.Id, correlationId, cancellationToken);
        if (request.ProcessSync)
        {
            var runJob = _jobFactory.CreateProcessDocumentJob(document.Id, correlationId);
            await runJob(cancellationToken);
        }
        return Result<UploadDocumentResult>.Success(new UploadDocumentResult(document.Id));
    }
}
