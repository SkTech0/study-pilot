using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StudyPilot.Application.Abstractions.Optimization;
using StudyPilot.Infrastructure.Services;

namespace StudyPilot.Infrastructure.Optimization;

public sealed class AutonomousOptimizer : IAutonomousOptimizer
{
    private const int SnapshotWindowSize = 10;
    private const int RollbackCheckSnapshots = 3;
    private const double LatencyPressureThresholdMs = 4000;
    private const double RetrievalHitRateLow = 0.55;
    private const double LatencyAcceptableMaxMs = 3500;
    private const double RollbackLatencyWorsenFactor = 1.25;
    private const int VectorTopKMin = 6;
    private const int VectorTopKMax = 20;
    private const double EmbeddingBatchReductionFactor = 0.8;
    private const double MaxAdjustmentFactor = 0.10;

    private readonly IServiceProvider _services;
    private readonly IOptimizationSafetyGuard _safetyGuard;
    private readonly IOptimizationConfigProvider _configProvider;
    private readonly ILogger<AutonomousOptimizer> _logger;
    private DateTime _lastAdjustmentUtc = DateTime.MinValue;

    public AutonomousOptimizer(
        IServiceProvider services,
        IOptimizationSafetyGuard safetyGuard,
        IOptimizationConfigProvider configProvider,
        ILogger<AutonomousOptimizer> logger)
    {
        _services = services;
        _safetyGuard = safetyGuard;
        _configProvider = configProvider;
        _logger = logger;
    }

    public async Task RunCycleAsync(CancellationToken cancellationToken = default)
    {
        if (_safetyGuard.ShouldFreezeOptimization())
        {
            _logger.LogInformation("Optimization cycle skipped: safety guard frozen");
            return;
        }

        await using var scope = _services.CreateAsyncScope();
        var snapshotRepo = scope.ServiceProvider.GetRequiredService<IOptimizationSnapshotRepository>();
        var configRepo = scope.ServiceProvider.GetRequiredService<IOptimizationConfigRepository>();

        var snapshots = await snapshotRepo.GetLastNAsync(SnapshotWindowSize + RollbackCheckSnapshots, cancellationToken);
        if (snapshots.Count < SnapshotWindowSize)
        {
            _logger.LogDebug("Insufficient snapshots for optimization: Count={Count}", snapshots.Count);
            return;
        }

        var recent = snapshots.Take(SnapshotWindowSize).ToList();
        var avgP95 = recent.Average(s => s.P95ChatLatencyMs);
        var avgRetrievalHitRate = recent.Average(s => s.RetrievalHitRate);
        var avgRetryRate = recent.Average(s => s.RetryRate);
        var avgQueueDepth = recent.Average(s => s.QueueDepth);
        var avgWaiters = recent.Average(s => s.AILimiterWaiters);
        var avgSuccessRate = recent.Average(s => s.SuccessRate);

        var current = await configRepo.GetSingleAsync(cancellationToken);
        if (current is null) return;

        if (ShouldRollback(snapshots, current))
        {
            var previous = await configRepo.GetPreviousVersionAsync(cancellationToken);
            if (previous is not null)
            {
                await configRepo.SaveWithHistoryAsync(previous with { LastUpdatedUtc = DateTime.UtcNow, Version = current.Version + 1 }, cancellationToken);
                StudyPilotMetrics.OptimizationRollbacksTotal.Add(1);
                _logger.LogWarning("optimization_rollback_triggered Reverted to Version={Version} VectorTopK={VectorTopK} ChunkSize={ChunkSize} MaxConcurrency={MaxConcurrency}",
                    previous.Version, previous.VectorTopK, previous.ChunkSizeTokens, previous.MaxAIConcurrency);
            }
            return;
        }

        OptimizationConfigDto? next = null;
        string? reason = null;

        if (avgP95 > LatencyPressureThresholdMs)
        {
            var newK = Math.Max(VectorTopKMin, current.VectorTopK - 1);
            if (newK != current.VectorTopK)
            {
                next = current with { VectorTopK = newK, LastUpdatedUtc = DateTime.UtcNow, Version = current.Version + 1 };
                reason = "latency_pressure";
            }
        }
        else if (avgRetrievalHitRate < RetrievalHitRateLow && avgP95 <= LatencyAcceptableMaxMs)
        {
            var newK = Math.Min(VectorTopKMax, current.VectorTopK + 1);
            if (newK != current.VectorTopK)
            {
                next = current with { VectorTopK = newK, LastUpdatedUtc = DateTime.UtcNow, Version = current.Version + 1 };
                reason = "low_retrieval_quality";
            }
        }
        else if (avgQueueDepth >= 20 || avgWaiters >= 2)
        {
            var newBatch = Math.Max(4, (int)(current.EmbeddingBatchSize * EmbeddingBatchReductionFactor));
            if (newBatch != current.EmbeddingBatchSize)
            {
                next = current with { EmbeddingBatchSize = newBatch, LastUpdatedUtc = DateTime.UtcNow, Version = current.Version + 1 };
                reason = "embedding_congestion";
            }
        }
        else if (avgWaiters == 0 && avgP95 < 2000 && recent.Take(3).All(s => s.P95ChatLatencyMs < 2500))
        {
            var newConcurrency = Math.Min(20, current.MaxAIConcurrency + 1);
            if (newConcurrency != current.MaxAIConcurrency)
            {
                next = current with { MaxAIConcurrency = newConcurrency, LastUpdatedUtc = DateTime.UtcNow, Version = current.Version + 1 };
                reason = "idle_capacity";
            }
        }
        else if (avgRetryRate > 0.15)
        {
            var newDelay = Math.Min(60, current.RetryBaseDelaySeconds + (int)Math.Ceiling(current.RetryBaseDelaySeconds * MaxAdjustmentFactor));
            if (newDelay != current.RetryBaseDelaySeconds)
            {
                next = current with { RetryBaseDelaySeconds = newDelay, LastUpdatedUtc = DateTime.UtcNow, Version = current.Version + 1 };
                reason = "retry_storm";
            }
        }

        if (next is not null && reason is not null)
        {
            await configRepo.SaveWithHistoryAsync(next, cancellationToken);
            _lastAdjustmentUtc = DateTime.UtcNow;
            StudyPilotMetrics.OptimizationAdjustmentsTotal.Add(1, new KeyValuePair<string, object?>("reason", reason));
            _logger.LogInformation("Optimization adjustment applied Reason={Reason} VectorTopK={VectorTopK} ChunkSize={ChunkSize} EmbeddingBatchSize={EmbeddingBatchSize} MaxConcurrency={MaxConcurrency} RetryBaseDelaySeconds={RetryBaseDelaySeconds}",
                reason, next.VectorTopK, next.ChunkSizeTokens, next.EmbeddingBatchSize, next.MaxAIConcurrency, next.RetryBaseDelaySeconds);
        }
    }

    private bool ShouldRollback(
        IReadOnlyList<OptimizationSnapshotDto> snapshots,
        OptimizationConfigDto current)
    {
        if (snapshots.Count < SnapshotWindowSize + RollbackCheckSnapshots) return false;
        var adjustedAt = current.LastUpdatedUtc;
        var after = snapshots.Where(s => s.CapturedAtUtc >= adjustedAt).OrderBy(s => s.CapturedAtUtc).Take(RollbackCheckSnapshots).ToList();
        var before = snapshots.Where(s => s.CapturedAtUtc < adjustedAt).OrderByDescending(s => s.CapturedAtUtc).Take(RollbackCheckSnapshots).ToList();
        if (after.Count < RollbackCheckSnapshots || before.Count < RollbackCheckSnapshots) return false;

        var avgP95Before = before.Average(s => s.P95ChatLatencyMs);
        var avgP95After = after.Average(s => s.P95ChatLatencyMs);
        var avgSuccessBefore = before.Average(s => s.SuccessRate);
        var avgSuccessAfter = after.Average(s => s.SuccessRate);

        if (avgP95Before > 0 && avgP95After > avgP95Before * RollbackLatencyWorsenFactor)
            return true;
        if (avgSuccessAfter < avgSuccessBefore)
            return true;
        return false;
    }
}
