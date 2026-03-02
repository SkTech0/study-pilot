using StudyPilot.Domain.Entities;

namespace StudyPilot.Application.Abstractions.Persistence;

public interface IConceptRepository
{
    Task<Concept?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Concept>> GetByDocumentIdAsync(Guid documentId, CancellationToken cancellationToken = default);
    Task AddAsync(Concept concept, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<Concept> concepts, CancellationToken cancellationToken = default);
Task<IReadOnlyList<Concept>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);
    Task DeleteByDocumentIdAsync(Guid documentId, CancellationToken cancellationToken = default);
}
