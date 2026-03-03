namespace StudyPilot.Application.Abstractions.Optimization;

public sealed record OptimizationSnapshotDto(
    DateTime CapturedAtUtc,
    double AvgChatLatencyMs,
    double P95ChatLatencyMs,
    double EmbeddingLatencyMs,
    double RetrievalHitRate,
    double RetryRate,
    int QueueDepth,
    int AILimiterWaiters,
    double TokenUsagePerMinute,
    double SuccessRate);
