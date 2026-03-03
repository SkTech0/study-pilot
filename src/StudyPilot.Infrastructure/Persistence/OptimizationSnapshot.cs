namespace StudyPilot.Infrastructure.Persistence;

public sealed class OptimizationSnapshot
{
    public long Id { get; set; }
    public DateTime CapturedAtUtc { get; set; }
    public double AvgChatLatencyMs { get; set; }
    public double P95ChatLatencyMs { get; set; }
    public double EmbeddingLatencyMs { get; set; }
    public double RetrievalHitRate { get; set; }
    public double RetryRate { get; set; }
    public int QueueDepth { get; set; }
    public int AILimiterWaiters { get; set; }
    public double TokenUsagePerMinute { get; set; }
    public double SuccessRate { get; set; }
}
