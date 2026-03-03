namespace StudyPilot.Infrastructure.Services;

public sealed class PipelineLoadOptions
{
    public const string SectionName = "KnowledgePipeline";

    /// <summary>Instance identifier for heartbeat (default: machine + process id).</summary>
    public string InstanceId { get; set; } = "";

    /// <summary>Outbox pending count above this → DEGRADED; above OutboxOverloadedThreshold → OVERLOADED.</summary>
    public int OutboxDegradedThreshold { get; set; } = 200;
    public int OutboxOverloadedThreshold { get; set; } = 500;

    /// <summary>Embedding queue depth above this → DEGRADED; above EmbeddingOverloadedThreshold → OVERLOADED.</summary>
    public int EmbeddingDegradedThreshold { get; set; } = 100;
    public int EmbeddingOverloadedThreshold { get; set; } = 300;

    /// <summary>AI limiter in-use slots; near max or waiters high → DEGRADED.</summary>
    public int AILimiterDegradedConcurrencyThreshold { get; set; } = 3;
    public int AILimiterWaitersDegradedThreshold { get; set; } = 2;
    public int AILimiterWaitersOverloadedThreshold { get; set; } = 5;

    /// <summary>Recovery actions in last window (rate); high rate → RECOVERY mode.</summary>
    public int RecoveryActionsRateDegradedThreshold { get; set; } = 50;
    public int RecoveryActionsRateOverloadedThreshold { get; set; } = 150;

    /// <summary>Daily AI token budget (estimated). Exceeding → DEGRADED.</summary>
    public long DailyTokenBudget { get; set; } = 1_000_000;

    /// <summary>Outbox dispatch batch size in NORMAL mode.</summary>
    public int OutboxDispatchBatchSizeNormal { get; set; } = 50;
    public int OutboxDispatchBatchSizeDegraded { get; set; } = 20;
    public int OutboxDispatchBatchSizeOverloaded { get; set; } = 5;

    /// <summary>Base poll delay for outbox dispatcher (seconds).</summary>
    public int OutboxDispatchPollBaseSeconds { get; set; } = 2;
    /// <summary>Multiplier for poll delay when overloaded.</summary>
    public double OutboxDispatchPollDelayMultiplierOverloaded { get; set; } = 4.0;
}
