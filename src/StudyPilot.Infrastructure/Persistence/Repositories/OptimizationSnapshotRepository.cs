using Microsoft.EntityFrameworkCore;
using StudyPilot.Application.Abstractions.Optimization;
using StudyPilot.Infrastructure.Persistence;
using StudyPilot.Infrastructure.Persistence.DbContext;

namespace StudyPilot.Infrastructure.Persistence.Repositories;

public sealed class OptimizationSnapshotRepository : IOptimizationSnapshotRepository
{
    private readonly StudyPilotDbContext _db;

    public OptimizationSnapshotRepository(StudyPilotDbContext db) => _db = db;

    public async Task InsertAsync(OptimizationSnapshotDto snapshot, CancellationToken cancellationToken = default)
    {
        var entity = new OptimizationSnapshot
        {
            CapturedAtUtc = snapshot.CapturedAtUtc,
            AvgChatLatencyMs = snapshot.AvgChatLatencyMs,
            P95ChatLatencyMs = snapshot.P95ChatLatencyMs,
            EmbeddingLatencyMs = snapshot.EmbeddingLatencyMs,
            RetrievalHitRate = snapshot.RetrievalHitRate,
            RetryRate = snapshot.RetryRate,
            QueueDepth = snapshot.QueueDepth,
            AILimiterWaiters = snapshot.AILimiterWaiters,
            TokenUsagePerMinute = snapshot.TokenUsagePerMinute,
            SuccessRate = snapshot.SuccessRate
        };
        await _db.OptimizationSnapshots.AddAsync(entity, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OptimizationSnapshotDto>> GetLastNAsync(int n, CancellationToken cancellationToken = default)
    {
        var list = await _db.OptimizationSnapshots
            .OrderByDescending(x => x.CapturedAtUtc)
            .Take(n)
            .ToListAsync(cancellationToken);
        return list.Select(x => new OptimizationSnapshotDto(
            x.CapturedAtUtc,
            x.AvgChatLatencyMs,
            x.P95ChatLatencyMs,
            x.EmbeddingLatencyMs,
            x.RetrievalHitRate,
            x.RetryRate,
            x.QueueDepth,
            x.AILimiterWaiters,
            x.TokenUsagePerMinute,
            x.SuccessRate)).ToList();
    }
}
