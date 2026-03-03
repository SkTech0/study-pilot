using StudyPilot.Domain.Entities;

namespace StudyPilot.Application.Abstractions.Persistence;

public interface IUserConceptMasteryRepository
{
    Task<UserConceptMastery?> GetByUserAndConceptAsync(Guid userId, Guid conceptId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserConceptMastery>> GetByUserAndConceptsAsync(Guid userId, IReadOnlyList<Guid> conceptIds, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserConceptMastery>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(UserConceptMastery entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(UserConceptMastery entity, CancellationToken cancellationToken = default);
    Task UpsertBatchAsync(IReadOnlyList<UserConceptMastery> entities, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Guid>> GetDistinctUserIdsAsync(CancellationToken cancellationToken = default);
}
