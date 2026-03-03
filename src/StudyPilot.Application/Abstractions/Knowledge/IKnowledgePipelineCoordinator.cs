namespace StudyPilot.Application.Abstractions.Knowledge;

/// <summary>
/// Central coordinator for knowledge pipeline. Workers query before scheduling work.
/// Lightweight, in-memory, observes queue depth, limiter usage, outbox backlog, recovery rate.
/// </summary>
public interface IKnowledgePipelineCoordinator
{
    /// <summary>Instance identifier for this process (heartbeat key).</summary>
    string InstanceId { get; }

    /// <summary>Local mode computed from this instance's load.</summary>
    PipelineMode LocalMode { get; }

    /// <summary>Global mode = worst(LocalMode) across active instances. Decisions use this.</summary>
    PipelineMode GlobalMode { get; }

    PipelineLoadSnapshot GetSnapshot();

    /// <summary>Called by heartbeat service after reading active heartbeats. No locks.</summary>
    void SetGlobalMode(PipelineMode mode);

    /// <summary>Called by heartbeat service with rolling 24h token sum from DB.</summary>
    void SetRolling24hTokenUsage(long totalTokens);

    /// <summary>Whether to allow new enrichment scheduling (outbox dispatch -> embedding queue). When overloaded, returns false.</summary>
    bool AllowEnrichmentScheduling();

    /// <summary>Whether recovery worker may enqueue new outbox entries or stale refresh. When overloaded, returns false.</summary>
    bool AllowRecoveryEnqueue();

    /// <summary>Outbox dispatch batch size (reduced when degraded/overloaded).</summary>
    int GetOutboxDispatchBatchSize();

    /// <summary>Delay before next outbox dispatch poll (increased when overloaded).</summary>
    TimeSpan GetOutboxDispatchPollDelay(TimeSpan baseDelay);

    /// <summary>Recovery worker batch limits: when degraded, prioritize READY completion over new work; when overloaded, pause enqueue.</summary>
    bool ShouldRecoveryPrioritizeReadyOnly();
    bool ShouldRecoveryPauseStaleRefresh();

    /// <summary>Record recovery actions for rate observation.</summary>
    void RecordRecoveryActions(int count);

    /// <summary>Record estimated token usage for daily budget.</summary>
    void RecordEstimatedTokenUsage(int estimatedTokens);

    /// <summary>Refresh snapshot from external metrics (call from workers or a timer).</summary>
    void UpdateLoad(int outboxPendingCount, int embeddingQueueDepth, int aiConcurrency, int aiWaiters);

    /// <summary>Whether LOW priority jobs are allowed (only when GlobalMode == Normal).</summary>
    bool AllowLowPriorityJobs { get; }

    /// <summary>Last known pending counts by priority [Critical, High, Normal, Low]. Updated by heartbeat.</summary>
    void SetPriorityQueueDepths(IReadOnlyList<int> countsByPriority);
}
