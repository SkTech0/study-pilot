using StudyPilot.Infrastructure.Persistence;

namespace StudyPilot.Infrastructure.Persistence.Repositories;

public interface IKnowledgeTokenUsageRepository
{
    Task AddAsync(KnowledgeTokenUsage usage, CancellationToken cancellationToken = default);
    Task<long> GetSumLast24HoursAsync(CancellationToken cancellationToken = default);
    Task<long> GetSumSinceAsync(DateTime sinceUtc, CancellationToken cancellationToken = default);
    Task PruneOlderThanAsync(TimeSpan olderThan, CancellationToken cancellationToken = default);
}
