using Microsoft.EntityFrameworkCore;
using StudyPilot.Infrastructure.Persistence.DbContext;

namespace StudyPilot.Infrastructure.Persistence.Repositories;

public interface IKnowledgeOutboxRepository
{
    Task AddAsync(KnowledgeOutboxEntry entry, CancellationToken cancellationToken = default);
    Task<int> GetPendingCountAsync(DateTime utcNow, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<KnowledgeOutboxEntry>> GetPendingBatchAsync(int maxItems, DateTime utcNow, CancellationToken cancellationToken = default);
    Task MarkProcessingAsync(Guid id, CancellationToken cancellationToken = default);
    Task MarkProcessedAsync(Guid id, CancellationToken cancellationToken = default);
    Task MarkFailedAsync(Guid id, bool allowRetry, DateTime? nextAttemptUtc, CancellationToken cancellationToken = default);
}

public sealed class KnowledgeOutboxRepository : IKnowledgeOutboxRepository
{
    private readonly StudyPilotDbContext _db;

    public KnowledgeOutboxRepository(StudyPilotDbContext db) => _db = db;

    public async Task AddAsync(KnowledgeOutboxEntry entry, CancellationToken cancellationToken = default)
    {
        await _db.KnowledgeOutboxEntries.AddAsync(entry, cancellationToken);
    }

    public async Task<int> GetPendingCountAsync(DateTime utcNow, CancellationToken cancellationToken = default)
    {
        return await _db.KnowledgeOutboxEntries
            .CountAsync(x =>
                (x.Status == "Pending" || x.Status == "Failed") &&
                (x.NextAttemptUtc == null || x.NextAttemptUtc <= utcNow),
                cancellationToken);
    }

    public async Task<IReadOnlyList<KnowledgeOutboxEntry>> GetPendingBatchAsync(int maxItems, DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var now = utcNow;
        var max = Math.Clamp(maxItems, 1, 500);

        return await _db.KnowledgeOutboxEntries
            .Where(x =>
                (x.Status == "Pending" || x.Status == "Failed") &&
                (x.NextAttemptUtc == null || x.NextAttemptUtc <= now))
            .OrderBy(x => x.CreatedUtc)
            .Take(max)
            .ToListAsync(cancellationToken);
    }

    public async Task MarkProcessingAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await _db.KnowledgeOutboxEntries
            .Where(x => x.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.Status, "Processing"), cancellationToken);
    }

    public async Task MarkProcessedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await _db.KnowledgeOutboxEntries
            .Where(x => x.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(e => e.Status, "Completed")
                .SetProperty(e => e.NextAttemptUtc, (DateTime?)null), cancellationToken);
    }

    public async Task MarkFailedAsync(Guid id, bool allowRetry, DateTime? nextAttemptUtc, CancellationToken cancellationToken = default)
    {
        var status = allowRetry ? "Pending" : "Failed";
        await _db.KnowledgeOutboxEntries
            .Where(x => x.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(e => e.Status, status)
                .SetProperty(e => e.RetryCount, e => e.RetryCount + 1)
                .SetProperty(e => e.NextAttemptUtc, nextAttemptUtc), cancellationToken);
    }
}

