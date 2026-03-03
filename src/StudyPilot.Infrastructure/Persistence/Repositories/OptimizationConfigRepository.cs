using Microsoft.EntityFrameworkCore;
using StudyPilot.Application.Abstractions.Optimization;
using StudyPilot.Infrastructure.Persistence;
using StudyPilot.Infrastructure.Persistence.DbContext;

namespace StudyPilot.Infrastructure.Persistence.Repositories;

public sealed class OptimizationConfigRepository : IOptimizationConfigRepository
{
    private const int SingleRowId = 1;
    private readonly StudyPilotDbContext _db;

    public OptimizationConfigRepository(StudyPilotDbContext db) => _db = db;

    public async Task<OptimizationConfigDto?> GetSingleAsync(CancellationToken cancellationToken = default)
    {
        var row = await _db.OptimizationConfigs.FindAsync([SingleRowId], cancellationToken);
        return row is null ? null : ToDto(row);
    }

    public async Task SaveAsync(OptimizationConfigDto config, CancellationToken cancellationToken = default)
    {
        var existing = await _db.OptimizationConfigs.FindAsync([SingleRowId], cancellationToken);
        if (existing is null)
        {
            _db.OptimizationConfigs.Add(new OptimizationConfig
            {
                Id = SingleRowId,
                ChunkSizeTokens = config.ChunkSizeTokens,
                VectorTopK = config.VectorTopK,
                EmbeddingBatchSize = config.EmbeddingBatchSize,
                MaxAIConcurrency = config.MaxAIConcurrency,
                RetryBaseDelaySeconds = config.RetryBaseDelaySeconds,
                LastUpdatedUtc = config.LastUpdatedUtc,
                Version = config.Version
            });
        }
        else
        {
            existing.ChunkSizeTokens = config.ChunkSizeTokens;
            existing.VectorTopK = config.VectorTopK;
            existing.EmbeddingBatchSize = config.EmbeddingBatchSize;
            existing.MaxAIConcurrency = config.MaxAIConcurrency;
            existing.RetryBaseDelaySeconds = config.RetryBaseDelaySeconds;
            existing.LastUpdatedUtc = config.LastUpdatedUtc;
            existing.Version = config.Version;
        }
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveWithHistoryAsync(OptimizationConfigDto config, CancellationToken cancellationToken = default)
    {
        var existing = await _db.OptimizationConfigs.FindAsync([SingleRowId], cancellationToken);
        if (existing is not null)
        {
            _db.OptimizationConfigHistory.Add(new OptimizationConfigHistory
            {
                AppliedAtUtc = existing.LastUpdatedUtc,
                ChunkSizeTokens = existing.ChunkSizeTokens,
                VectorTopK = existing.VectorTopK,
                EmbeddingBatchSize = existing.EmbeddingBatchSize,
                MaxAIConcurrency = existing.MaxAIConcurrency,
                RetryBaseDelaySeconds = existing.RetryBaseDelaySeconds,
                Version = existing.Version
            });
        }
        await SaveAsync(config, cancellationToken);
    }

    public async Task<OptimizationConfigDto?> GetPreviousVersionAsync(CancellationToken cancellationToken = default)
    {
        var row = await _db.OptimizationConfigHistory
            .OrderByDescending(x => x.AppliedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        return row is null ? null : new OptimizationConfigDto(
            row.ChunkSizeTokens,
            row.VectorTopK,
            row.EmbeddingBatchSize,
            row.MaxAIConcurrency,
            row.RetryBaseDelaySeconds,
            row.AppliedAtUtc,
            row.Version);
    }

    private static OptimizationConfigDto ToDto(OptimizationConfig row) =>
        new(row.ChunkSizeTokens, row.VectorTopK, row.EmbeddingBatchSize, row.MaxAIConcurrency,
            row.RetryBaseDelaySeconds, row.LastUpdatedUtc, row.Version);
}
