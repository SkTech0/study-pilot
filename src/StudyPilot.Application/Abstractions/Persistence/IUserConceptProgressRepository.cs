using StudyPilot.Domain.Entities;

namespace StudyPilot.Application.Abstractions.Persistence;

public interface IUserConceptProgressRepository
{
    Task<UserConceptProgress?> GetByUserAndConceptAsync(Guid userId, Guid conceptId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserConceptProgress>> GetWeakByUserIdAsync(Guid userId, int threshold, CancellationToken cancellationToken = default);
    Task AddAsync(UserConceptProgress progress, CancellationToken cancellationToken = default);
    Task UpdateAsync(UserConceptProgress progress, CancellationToken cancellationToken = default);
}
