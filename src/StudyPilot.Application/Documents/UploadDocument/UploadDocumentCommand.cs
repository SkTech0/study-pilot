using MediatR;
using StudyPilot.Application.Common.Models;

namespace StudyPilot.Application.Documents.UploadDocument;

/// <param name="ProcessSync">When true, process the document inline (e.g. in Development) so it completes before upload response. Default false.</param>
public sealed record UploadDocumentCommand(Guid UserId, string FileName, string StoragePath, bool ProcessSync = false) : IRequest<Result<UploadDocumentResult>>;

public sealed record UploadDocumentResult(Guid DocumentId);
