using MediatR;
using StudyPilot.Application.Abstractions.Persistence;
using StudyPilot.Application.Common.Models;
using StudyPilot.Domain.Enums;

namespace StudyPilot.Application.Documents.GetDocuments;

public sealed class GetDocumentsQueryHandler : IRequestHandler<GetDocumentsQuery, Result<IReadOnlyList<DocumentListItem>>>
{
    private readonly IDocumentRepository _documentRepository;

    public GetDocumentsQueryHandler(IDocumentRepository documentRepository) => _documentRepository = documentRepository;

    public async Task<Result<IReadOnlyList<DocumentListItem>>> Handle(GetDocumentsQuery request, CancellationToken cancellationToken)
    {
        var documents = await _documentRepository.GetByUserIdAsync(request.UserId, cancellationToken);
        var items = documents
            .Select(d => new DocumentListItem(d.Id, d.FileName, d.ProcessingStatus.ToString(), d.CreatedAtUtc, d.FailureReason))
            .ToList();
        return Result<IReadOnlyList<DocumentListItem>>.Success(items);
    }
}
