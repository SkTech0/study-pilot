using Microsoft.EntityFrameworkCore;
using StudyPilot.Infrastructure.Persistence.DbContext;

namespace StudyPilot.Infrastructure.Persistence.Repositories;

public interface IBackgroundJobRepository
{
    Task AddAsync(BackgroundJob job, CancellationToken cancellationToken = default);
    Task<BackgroundJob?> TryClaimNextAsync(string workerId, TimeSpan processingTimeout, int maxRetries, CancellationToken cancellationToken = default);
    Task MarkCompletedAsync(Guid jobId, CancellationToken cancellationToken = default);
    Task MarkFailedAsync(Guid jobId, string? errorMessage, bool allowRetry, DateTime? nextRetryAtUtc, CancellationToken cancellationToken = default);
    Task ReleaseStuckJobsAsync(TimeSpan processingTimeout, CancellationToken cancellationToken = default);
    Task<bool> ExistsPendingOrProcessingForDocumentAsync(Guid documentId, CancellationToken cancellationToken = default);
    Task<int> GetPendingCountAsync(CancellationToken cancellationToken = default);
}

public sealed class BackgroundJobRepository : IBackgroundJobRepository
{
    private readonly StudyPilotDbContext _db;

    public BackgroundJobRepository(StudyPilotDbContext db) => _db = db;

    public async Task AddAsync(BackgroundJob job, CancellationToken cancellationToken = default)
    {
        await _db.BackgroundJobs.AddAsync(job, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<BackgroundJob?> TryClaimNextAsync(string workerId, TimeSpan processingTimeout, int maxRetries, CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow.Add(-processingTimeout);
        var now = DateTime.UtcNow;
        Guid claimedId = default;
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var ids = await _db.Database
                .SqlQueryRaw<Guid>(@"
SELECT ""Id"" FROM ""BackgroundJobs""
WHERE (""Status"" = 'Pending' OR (""Status"" = 'Processing' AND ""ClaimedAtUtc"" < {0}))
AND (""NextRetryAtUtc"" IS NULL OR ""NextRetryAtUtc"" <= {1})
AND ""RetryCount"" < {2}
ORDER BY ""CreatedAtUtc""
LIMIT 1
FOR UPDATE SKIP LOCKED", cutoff, now, maxRetries)
                .ToListAsync(cancellationToken);
            if (ids.Count == 0)
            {
                await transaction.CommitAsync(cancellationToken);
                return null;
            }
            claimedId = ids[0];
            await _db.BackgroundJobs
                .Where(j => j.Id == claimedId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(j => j.Status, "Processing")
                    .SetProperty(j => j.ClaimedAtUtc, now)
                    .SetProperty(j => j.ClaimedBy, workerId), cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
        return await _db.BackgroundJobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == claimedId, cancellationToken);
    }

    public async Task MarkCompletedAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        await _db.BackgroundJobs
            .Where(j => j.Id == jobId)
            .ExecuteUpdateAsync(s => s.SetProperty(j => j.Status, "Completed"), cancellationToken);
    }

    public async Task MarkFailedAsync(Guid jobId, string? errorMessage, bool allowRetry, DateTime? nextRetryAtUtc, CancellationToken cancellationToken = default)
    {
        var status = allowRetry ? "Pending" : "Failed";
        var err = string.IsNullOrEmpty(errorMessage) ? "" : (errorMessage.Length > 1000 ? errorMessage[..1000] : errorMessage);
        if (allowRetry)
        {
            await _db.Database.ExecuteSqlRawAsync(@"
UPDATE ""BackgroundJobs""
SET ""Status"" = {0}, ""ErrorMessage"" = {1}, ""NextRetryAtUtc"" = {2}, ""ClaimedAtUtc"" = NULL, ""ClaimedBy"" = NULL, ""RetryCount"" = ""RetryCount"" + 1
WHERE ""Id"" = {3}", status, err, nextRetryAtUtc ?? (object)DBNull.Value, jobId, cancellationToken);
        }
        else
        {
            await _db.BackgroundJobs
                .Where(j => j.Id == jobId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(j => j.Status, status)
                    .SetProperty(j => j.ErrorMessage, err)
                    .SetProperty(j => j.NextRetryAtUtc, nextRetryAtUtc)
                    .SetProperty(j => j.ClaimedAtUtc, (DateTime?)null)
                    .SetProperty(j => j.ClaimedBy, (string?)null), cancellationToken);
        }
    }

    public async Task ReleaseStuckJobsAsync(TimeSpan processingTimeout, CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow.Add(-processingTimeout);
        await _db.BackgroundJobs
            .Where(j => j.Status == "Processing" && j.ClaimedAtUtc != null && j.ClaimedAtUtc < cutoff)
            .ExecuteUpdateAsync(s => s
                .SetProperty(j => j.Status, "Pending")
                .SetProperty(j => j.ClaimedAtUtc, (DateTime?)null)
                .SetProperty(j => j.ClaimedBy, (string?)null), cancellationToken);
    }

    public async Task<bool> ExistsPendingOrProcessingForDocumentAsync(Guid documentId, CancellationToken cancellationToken = default) =>
        await _db.BackgroundJobs.AnyAsync(j => j.DocumentId == documentId && (j.Status == "Pending" || j.Status == "Processing"), cancellationToken);

    public async Task<int> GetPendingCountAsync(CancellationToken cancellationToken = default) =>
        await _db.BackgroundJobs.CountAsync(j => j.Status == "Pending" || j.Status == "Processing", cancellationToken);
}
