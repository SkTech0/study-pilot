using Microsoft.EntityFrameworkCore;
using StudyPilot.Infrastructure.Persistence;
using StudyPilot.Infrastructure.Persistence.DbContext;

namespace StudyPilot.Infrastructure.Persistence.Repositories;

public sealed class KnowledgeTokenUsageRepository : IKnowledgeTokenUsageRepository
{
    private readonly StudyPilotDbContext _db;

    public KnowledgeTokenUsageRepository(StudyPilotDbContext db) => _db = db;

    public async Task AddAsync(KnowledgeTokenUsage usage, CancellationToken cancellationToken = default)
    {
        await _db.KnowledgeTokenUsage.AddAsync(usage, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<long> GetSumLast24HoursAsync(CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow.AddHours(-24);
        return await _db.KnowledgeTokenUsage
            .Where(u => u.TimestampUtc >= cutoff)
            .SumAsync(u => u.EstimatedTokens, cancellationToken);
    }

    public async Task<long> GetSumSinceAsync(DateTime sinceUtc, CancellationToken cancellationToken = default)
    {
        return await _db.KnowledgeTokenUsage
            .Where(u => u.TimestampUtc >= sinceUtc)
            .SumAsync(u => u.EstimatedTokens, cancellationToken);
    }

    public async Task PruneOlderThanAsync(TimeSpan olderThan, CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow - olderThan;
        await _db.KnowledgeTokenUsage.Where(u => u.TimestampUtc < cutoff).ExecuteDeleteAsync(cancellationToken);
    }
}
