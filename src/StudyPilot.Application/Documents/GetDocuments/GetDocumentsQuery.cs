using MediatR;
using StudyPilot.Application.Common.Models;

namespace StudyPilot.Application.Documents.GetDocuments;

public sealed record DocumentListItem(Guid Id, string FileName, string Status, DateTime CreatedAtUtc);

public sealed record GetDocumentsQuery(Guid UserId) : IRequest<Result<IReadOnlyList<DocumentListItem>>>;
