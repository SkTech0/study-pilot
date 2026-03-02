using StudyPilot.Domain.Entities;

namespace StudyPilot.Application.Abstractions.Persistence;

public interface IDocumentChunkRepository
{
    Task DeleteByDocumentIdAsync(Guid documentId, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IReadOnlyList<DocumentChunk> chunks, CancellationToken cancellationToken = default);
}

