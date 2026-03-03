namespace StudyPilot.Infrastructure.Persistence;

/// <summary>
/// Per-instance heartbeat for distributed pipeline awareness. Updated every ~10s.
/// Active = LastSeenUtc within 30 seconds. Global mode = worst(CurrentMode) across active.
/// </summary>
public sealed class KnowledgePipelineHeartbeat
{
    public string InstanceId { get; set; } = string.Empty;
    public DateTime LastSeenUtc { get; set; }
    public int CurrentMode { get; set; }
    public int OutboxPending { get; set; }
    public int EmbeddingDepth { get; set; }
    public int AILimiterWaiters { get; set; }
}
