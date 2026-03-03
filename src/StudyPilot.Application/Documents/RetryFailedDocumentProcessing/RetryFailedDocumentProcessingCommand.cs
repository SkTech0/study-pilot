using MediatR;
using StudyPilot.Application.Common.Models;

namespace StudyPilot.Application.Documents.RetryFailedDocumentProcessing;

public sealed record RetryFailedDocumentProcessingCommand : IRequest<Result<RetryFailedDocumentProcessingResult>>;

public sealed record RetryFailedDocumentProcessingResult(int DocumentsReset, int JobsReset);
