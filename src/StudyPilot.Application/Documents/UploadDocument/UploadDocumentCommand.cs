using MediatR;
using StudyPilot.Application.Common.Models;

namespace StudyPilot.Application.Documents.UploadDocument;

public sealed record UploadDocumentCommand(Guid UserId, string FileName, string StoragePath) : IRequest<Result<UploadDocumentResult>>;

public sealed record UploadDocumentResult(Guid DocumentId);
