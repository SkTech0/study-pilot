namespace StudyPilot.Application.Abstractions.Knowledge;

/// <summary>
/// Lightweight snapshot of pipeline load for coordinator decisions.
/// </summary>
public sealed class PipelineLoadSnapshot
{
    public int OutboxPendingCount { get; init; }
    public int EmbeddingQueueDepth { get; init; }
    public int AILimiterConcurrency { get; init; }
    public int AILimiterWaiters { get; init; }
    public int RecoveryActionsRate { get; init; }
    public double EstimatedDailyTokenUsage { get; init; }
    public PipelineMode Mode { get; init; }
}
