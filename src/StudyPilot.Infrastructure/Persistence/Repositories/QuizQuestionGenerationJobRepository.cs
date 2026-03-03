using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StudyPilot.Infrastructure.Persistence.DbContext;

namespace StudyPilot.Infrastructure.Persistence.Repositories;

public interface IQuizQuestionGenerationJobRepository
{
    Task<Guid> AddAsync(QuizQuestionGenerationJob job, CancellationToken cancellationToken = default);
    Task<QuizQuestionGenerationJob?> TryClaimNextAsync(string workerId, TimeSpan processingTimeout, int maxRetries, CancellationToken cancellationToken = default);
    Task MarkCompletedAsync(Guid jobId, CancellationToken cancellationToken = default);
    Task MarkFailedAsync(Guid jobId, string? errorMessage, bool allowRetry, DateTime? nextRetryAtUtc, CancellationToken cancellationToken = default);
    Task ReleaseStuckJobsAsync(TimeSpan processingTimeout, CancellationToken cancellationToken = default);
    Task<int> GetPendingCountAsync(CancellationToken cancellationToken = default);
}

public sealed class QuizQuestionGenerationJobRepository : IQuizQuestionGenerationJobRepository
{
    private readonly StudyPilotDbContext _db;
    private readonly ILogger<QuizQuestionGenerationJobRepository> _logger;

    public QuizQuestionGenerationJobRepository(StudyPilotDbContext db, ILogger<QuizQuestionGenerationJobRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<Guid> AddAsync(QuizQuestionGenerationJob job, CancellationToken cancellationToken = default)
    {
        await _db.QuizQuestionGenerationJobs.AddAsync(job, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return job.Id;
    }

    public async Task<QuizQuestionGenerationJob?> TryClaimNextAsync(string workerId, TimeSpan processingTimeout, int maxRetries, CancellationToken cancellationToken = default)
    {
        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            var cutoff = DateTime.UtcNow.Add(-processingTimeout);
            var now = DateTime.UtcNow;
            Guid claimedId = default;
            await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var ids = await _db.Database
                    .SqlQueryRaw<Guid>(@"
SELECT ""Id"" FROM ""QuizQuestionGenerationJobs""
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
                await _db.QuizQuestionGenerationJobs
                    .Where(j => j.Id == claimedId)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(j => j.Status, "Processing")
                        .SetProperty(j => j.ClaimedAtUtc, now)
                        .SetProperty(j => j.ClaimedBy, workerId), cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogError(ex, "TryClaimNextAsync failed JobType=QuizQuestionGeneration");
                throw;
            }
            return await _db.QuizQuestionGenerationJobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == claimedId, cancellationToken);
        });
    }

    public async Task MarkCompletedAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        await _db.QuizQuestionGenerationJobs
            .Where(j => j.Id == jobId)
            .ExecuteUpdateAsync(s => s.SetProperty(j => j.Status, "Completed"), cancellationToken);
    }

    public async Task MarkFailedAsync(Guid jobId, string? errorMessage, bool allowRetry, DateTime? nextRetryAtUtc, CancellationToken cancellationToken = default)
    {
        var err = string.IsNullOrEmpty(errorMessage) ? "" : (errorMessage.Length > 1000 ? errorMessage[..1000] : errorMessage);
        if (allowRetry)
        {
            await _db.QuizQuestionGenerationJobs
                .Where(j => j.Id == jobId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(j => j.Status, "Pending")
                    .SetProperty(j => j.ErrorMessage, err)
                    .SetProperty(j => j.NextRetryAtUtc, nextRetryAtUtc)
                    .SetProperty(j => j.ClaimedAtUtc, (DateTime?)null)
                    .SetProperty(j => j.ClaimedBy, (string?)null)
                    .SetProperty(j => j.RetryCount, j => j.RetryCount + 1), cancellationToken);
        }
        else
        {
            await _db.QuizQuestionGenerationJobs
                .Where(j => j.Id == jobId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(j => j.Status, "Failed")
                    .SetProperty(j => j.ErrorMessage, err), cancellationToken);
        }
    }

    public async Task ReleaseStuckJobsAsync(TimeSpan processingTimeout, CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow.Add(-processingTimeout);
        await _db.QuizQuestionGenerationJobs
            .Where(j => j.Status == "Processing" && j.ClaimedAtUtc != null && j.ClaimedAtUtc < cutoff)
            .ExecuteUpdateAsync(s => s
                .SetProperty(j => j.Status, "Pending")
                .SetProperty(j => j.ClaimedAtUtc, (DateTime?)null)
                .SetProperty(j => j.ClaimedBy, (string?)null), cancellationToken);
    }

    public async Task<int> GetPendingCountAsync(CancellationToken cancellationToken = default) =>
        await _db.QuizQuestionGenerationJobs.CountAsync(j => j.Status == "Pending" || j.Status == "Processing", cancellationToken);
}
