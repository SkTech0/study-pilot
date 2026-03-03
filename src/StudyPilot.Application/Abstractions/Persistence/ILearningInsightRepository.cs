using StudyPilot.Domain.Entities;
using StudyPilot.Domain.Enums;

namespace StudyPilot.Application.Abstractions.Persistence;

public interface ILearningInsightRepository
{
    Task AddAsync(LearningInsight insight, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IReadOnlyList<LearningInsight> insights, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LearningInsight>> GetByUserIdAsync(Guid userId, int limit, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid userId, Guid conceptId, LearningInsightType type, DateTime sinceUtc, CancellationToken cancellationToken = default);
    Task<IReadOnlySet<(Guid UserId, Guid ConceptId, LearningInsightType Type)>> GetExistingKeysAsync(IReadOnlyList<(Guid UserId, Guid ConceptId)> keys, DateTime sinceUtc, CancellationToken cancellationToken = default);
}
