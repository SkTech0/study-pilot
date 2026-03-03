using System.Diagnostics.Metrics;
using Microsoft.Extensions.Options;
using StudyPilot.Application.Abstractions.AI;
using StudyPilot.Application.Abstractions.Knowledge;
using StudyPilot.Infrastructure.Services;

namespace StudyPilot.Infrastructure.Knowledge;

public sealed class KnowledgePipelineCoordinator : IKnowledgePipelineCoordinator
{
    private readonly PipelineLoadOptions _options;
    private readonly IAIExecutionLimiter _limiter;
    private readonly Meter _meter;
    private readonly Counter<long> _loadSheddingEvents;
    private readonly ObservableGauge<int> _pipelineModeGauge;

    private int _outboxPendingCount;
    private int _embeddingQueueDepth;
    private int _recoveryActionsInWindow;
    private DateTime _recoveryWindowStartUtc = DateTime.UtcNow;
    private const int RecoveryWindowSeconds = 60;
    private long _estimatedDailyTokenUsage;
    private int _localModeInt = (int)PipelineMode.Normal;
    private int _globalModeInt = (int)PipelineMode.Normal;
    private long _rolling24hTokenUsage;
    private readonly object _gate = new();
    private readonly string _instanceId;

    public KnowledgePipelineCoordinator(
        IOptions<PipelineLoadOptions> options,
        IAIExecutionLimiter limiter)
    {
        _options = options.Value;
        _limiter = limiter;
        _instanceId = !string.IsNullOrWhiteSpace(_options.InstanceId)
            ? _options.InstanceId.Trim()
            : $"{Environment.MachineName}-{Environment.ProcessId}";
        _meter = new Meter(StudyPilotMetrics.MeterName, "1.0");
        _loadSheddingEvents = _meter.CreateCounter<long>("knowledge_load_shedding_events");
        _pipelineModeGauge = _meter.CreateObservableGauge("knowledge_pipeline_mode", () => (int)LocalMode);
        _ = _meter.CreateObservableGauge("knowledge_global_pipeline_mode", () => (int)GlobalMode);
        _ = _meter.CreateObservableGauge("knowledge_ai_budget_usage", () => (double)Volatile.Read(ref _rolling24hTokenUsage));
        _ = _meter.CreateObservableGauge("knowledge_token_usage_rolling", () => (double)Volatile.Read(ref _rolling24hTokenUsage));
        _ = _meter.CreateObservableGauge("knowledge_priority_queue_depth", () => GetPriorityQueueDepths());
    }

    private long _tokenRateEmaBits;
    private int _priorityDepth0;
    private int _priorityDepth1;
    private int _priorityDepth2;
    private int _priorityDepth3;

    private IEnumerable<Measurement<int>> GetPriorityQueueDepths()
    {
        return [
            new Measurement<int>(Volatile.Read(ref _priorityDepth0), new KeyValuePair<string, object?>("priority", "Critical")),
            new Measurement<int>(Volatile.Read(ref _priorityDepth1), new KeyValuePair<string, object?>("priority", "High")),
            new Measurement<int>(Volatile.Read(ref _priorityDepth2), new KeyValuePair<string, object?>("priority", "Normal")),
            new Measurement<int>(Volatile.Read(ref _priorityDepth3), new KeyValuePair<string, object?>("priority", "Low"))
        ];
    }

    public void SetPriorityQueueDepths(IReadOnlyList<int> countsByPriority)
    {
        if (countsByPriority is null || countsByPriority.Count < 4) return;
        Volatile.Write(ref _priorityDepth0, countsByPriority[0]);
        Volatile.Write(ref _priorityDepth1, countsByPriority[1]);
        Volatile.Write(ref _priorityDepth2, countsByPriority[2]);
        Volatile.Write(ref _priorityDepth3, countsByPriority[3]);
    }

    public string InstanceId => _instanceId;
    public PipelineMode LocalMode => (PipelineMode)Volatile.Read(ref _localModeInt);
    public PipelineMode GlobalMode => (PipelineMode)Volatile.Read(ref _globalModeInt);
    public bool AllowLowPriorityJobs => GlobalMode == PipelineMode.Normal;

    public void SetGlobalMode(PipelineMode mode)
    {
        Volatile.Write(ref _globalModeInt, (int)mode);
    }

    public void SetRolling24hTokenUsage(long totalTokens)
    {
        Volatile.Write(ref _rolling24hTokenUsage, totalTokens);
        var rate = totalTokens / 24.0;
        var prevBits = Volatile.Read(ref _tokenRateEmaBits);
        var prevEma = prevBits == 0 ? rate : BitConverter.Int64BitsToDouble(prevBits);
        var ema = 0.2 * rate + 0.8 * prevEma;
        Volatile.Write(ref _tokenRateEmaBits, BitConverter.DoubleToInt64Bits(ema));
    }

    public PipelineLoadSnapshot GetSnapshot()
    {
        return new PipelineLoadSnapshot
        {
            OutboxPendingCount = Volatile.Read(ref _outboxPendingCount),
            EmbeddingQueueDepth = Volatile.Read(ref _embeddingQueueDepth),
            AILimiterConcurrency = _limiter.CurrentConcurrency,
            AILimiterWaiters = _limiter.WaitersCount,
            RecoveryActionsRate = Volatile.Read(ref _recoveryActionsInWindow),
            EstimatedDailyTokenUsage = (double)Volatile.Read(ref _rolling24hTokenUsage),
            Mode = GlobalMode
        };
    }

    public bool AllowEnrichmentScheduling()
    {
        return GlobalMode != PipelineMode.Overloaded;
    }

    public bool AllowRecoveryEnqueue()
    {
        return GlobalMode != PipelineMode.Overloaded;
    }

    public int GetOutboxDispatchBatchSize()
    {
        return GlobalMode switch
        {
            PipelineMode.Overloaded => _options.OutboxDispatchBatchSizeOverloaded,
            PipelineMode.Degraded => _options.OutboxDispatchBatchSizeDegraded,
            _ => _options.OutboxDispatchBatchSizeNormal
        };
    }

    public TimeSpan GetOutboxDispatchPollDelay(TimeSpan baseDelay)
    {
        if (GlobalMode == PipelineMode.Overloaded)
            return TimeSpan.FromSeconds(baseDelay.TotalSeconds * _options.OutboxDispatchPollDelayMultiplierOverloaded);
        return baseDelay;
    }

    public bool ShouldRecoveryPrioritizeReadyOnly()
    {
        return GlobalMode == PipelineMode.Degraded || GlobalMode == PipelineMode.Overloaded;
    }

    public bool ShouldRecoveryPauseStaleRefresh()
    {
        return GlobalMode == PipelineMode.Degraded || GlobalMode == PipelineMode.Overloaded;
    }

    public void RecordRecoveryActions(int count)
    {
        if (count <= 0) return;
        lock (_gate)
        {
            var now = DateTime.UtcNow;
            if ((now - _recoveryWindowStartUtc).TotalSeconds >= RecoveryWindowSeconds)
            {
                _recoveryActionsInWindow = 0;
                _recoveryWindowStartUtc = now;
            }
            _recoveryActionsInWindow += count;
        }
    }

    public void RecordEstimatedTokenUsage(int estimatedTokens)
    {
        if (estimatedTokens <= 0) return;
        Interlocked.Add(ref _estimatedDailyTokenUsage, estimatedTokens);
    }

    public void UpdateLoad(int outboxPendingCount, int embeddingQueueDepth, int aiConcurrency, int aiWaiters)
    {
        Volatile.Write(ref _outboxPendingCount, outboxPendingCount);
        Volatile.Write(ref _embeddingQueueDepth, embeddingQueueDepth);
        var mode = ComputeMode(outboxPendingCount, embeddingQueueDepth, aiConcurrency, aiWaiters);
        var previous = (PipelineMode)Volatile.Read(ref _localModeInt);
        Volatile.Write(ref _localModeInt, (int)mode);
        if (mode != previous && (mode == PipelineMode.Overloaded || mode == PipelineMode.Degraded))
            _loadSheddingEvents.Add(1, new KeyValuePair<string, object?>("mode", mode.ToString()));
    }

    private PipelineMode ComputeMode(int outboxPending, int embeddingDepth, int aiConcurrency, int aiWaiters)
    {
        var recoveryRate = Volatile.Read(ref _recoveryActionsInWindow);
        if ((DateTime.UtcNow - _recoveryWindowStartUtc).TotalSeconds >= RecoveryWindowSeconds)
            recoveryRate = 0;

        var rollingTokens = (double)Volatile.Read(ref _rolling24hTokenUsage);
        var budgetExceeded = _options.DailyTokenBudget > 0 && rollingTokens >= _options.DailyTokenBudget;

        var overloaded = outboxPending >= _options.OutboxOverloadedThreshold
                         || embeddingDepth >= _options.EmbeddingOverloadedThreshold
                         || aiWaiters >= _options.AILimiterWaitersOverloadedThreshold
                         || recoveryRate >= _options.RecoveryActionsRateOverloadedThreshold;

        if (overloaded) return PipelineMode.Overloaded;

        var degraded = outboxPending >= _options.OutboxDegradedThreshold
                       || embeddingDepth >= _options.EmbeddingDegradedThreshold
                       || aiConcurrency >= _options.AILimiterDegradedConcurrencyThreshold
                       || aiWaiters >= _options.AILimiterWaitersDegradedThreshold
                       || recoveryRate >= _options.RecoveryActionsRateDegradedThreshold
                       || budgetExceeded;

        if (degraded) return PipelineMode.Degraded;

        if (recoveryRate > 0 && recoveryRate < _options.RecoveryActionsRateDegradedThreshold)
            return PipelineMode.Recovery;

        return PipelineMode.Normal;
    }
}
