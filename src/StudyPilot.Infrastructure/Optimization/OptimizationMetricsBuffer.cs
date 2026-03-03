using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace StudyPilot.Infrastructure.Optimization;

/// <summary>
/// Thread-safe rolling buffer for latency and rate metrics. Consumed by OptimizationMetricsCollector every 60s.
/// Populated via MeterListener from StudyPilotMetrics histograms so we can compute avg/P95 without changing call sites.
/// </summary>
public sealed class OptimizationMetricsBuffer
{
    private const int MaxSamples = 2000;
    private readonly ConcurrentQueue<double> _chatLatencyMs = new();
    private readonly ConcurrentQueue<double> _embeddingLatencyMs = new();
    private readonly object _lock = new();
    private int _retrievalRequests;
    private int _retrievalHits;
    private int _retryCount;
    private int _successCount;
    private long _tokensLastMinute;

    public void RecordChatLatencyMs(double value)
    {
        EnqueueBounded(_chatLatencyMs, value);
    }

    public void RecordEmbeddingLatencyMs(double value)
    {
        EnqueueBounded(_embeddingLatencyMs, value);
    }

    public void RecordRetrieval(bool isHit)
    {
        Interlocked.Increment(ref _retrievalRequests);
        if (isHit) Interlocked.Increment(ref _retrievalHits);
    }

    public void RecordRetry() => Interlocked.Increment(ref _retryCount);
    public void RecordSuccess() => Interlocked.Increment(ref _successCount);
    public void RecordTokens(long count) => Interlocked.Add(ref _tokensLastMinute, count);

    public (double AvgChatMs, double P95ChatMs, double AvgEmbeddingMs, double RetrievalHitRate, double RetryRate, double TokenUsagePerMinute, double SuccessRate) GetAndReset()
    {
        var chatList = Drain(_chatLatencyMs);
        var embedList = Drain(_embeddingLatencyMs);

        var avgChat = chatList.Count > 0 ? chatList.Average() : 0;
        var p95Chat = Percentile(chatList, 0.95);
        var avgEmbed = embedList.Count > 0 ? embedList.Average() : 0;

        var req = Interlocked.Exchange(ref _retrievalRequests, 0);
        var hits = Interlocked.Exchange(ref _retrievalHits, 0);
        var retrievalHitRate = req > 0 ? (double)hits / req : 0;

        var retries = Interlocked.Exchange(ref _retryCount, 0);
        var successes = Interlocked.Exchange(ref _successCount, 0);
        var total = retries + successes;
        var retryRate = total > 0 ? (double)retries / total : 0;
        var successRate = total > 0 ? (double)successes / total : 1;

        var tokens = Interlocked.Exchange(ref _tokensLastMinute, 0);

        return (avgChat, p95Chat, avgEmbed, retrievalHitRate, retryRate, tokens, successRate);
    }

    private static void EnqueueBounded(ConcurrentQueue<double> queue, double value)
    {
        queue.Enqueue(value);
        while (queue.Count > MaxSamples && queue.TryDequeue(out _)) { }
    }

    private static List<double> Drain(ConcurrentQueue<double> queue)
    {
        var list = new List<double>();
        while (queue.TryDequeue(out var v)) list.Add(v);
        return list;
    }

    private static double Percentile(List<double> sorted, double p)
    {
        if (sorted.Count == 0) return 0;
        var sortedCopy = new List<double>(sorted);
        sortedCopy.Sort();
        var idx = (int)Math.Ceiling(p * sortedCopy.Count) - 1;
        idx = Math.Max(0, idx);
        return sortedCopy[idx];
    }
}
