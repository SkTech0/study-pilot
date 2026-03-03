using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StudyPilot.Application.Abstractions.Knowledge;
using StudyPilot.Application.Abstractions.Optimization;
using StudyPilot.Infrastructure.Services;

namespace StudyPilot.Infrastructure.Optimization;

public sealed class OptimizationMetricsCollector : BackgroundService
{
    private const int IntervalSeconds = 60;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly OptimizationMetricsBuffer _buffer;
    private readonly IKnowledgePipelineCoordinator _coordinator;
    private readonly ILogger<OptimizationMetricsCollector> _logger;
    private MeterListener? _listener;

    public OptimizationMetricsCollector(
        IServiceScopeFactory scopeFactory,
        OptimizationMetricsBuffer buffer,
        IKnowledgePipelineCoordinator coordinator,
        ILogger<OptimizationMetricsCollector> logger)
    {
        _scopeFactory = scopeFactory;
        _buffer = buffer;
        _coordinator = coordinator;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        StartMeterListener();
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(IntervalSeconds), stoppingToken);
                await CollectAndPersistAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OptimizationMetricsCollector cycle failed");
            }
        }
        _listener?.Dispose();
    }

    private void StartMeterListener()
    {
        _listener = new MeterListener();
        _listener.SetMeasurementEventCallback<double>(OnMeasurement);
        _listener.EnableMeasurementEvents(StudyPilotMetrics.AIRequestDurationMs);
        _listener.EnableMeasurementEvents(StudyPilotMetrics.KnowledgeEmbeddingLatency);
        _listener.Start();
    }

    private void OnMeasurement(Instrument instrument, double measurement, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
    {
        if (instrument.Name == "ai_request_duration_ms")
        {
            var op = GetTagValue(tags, "operation");
            if (op is "chat" or "chat_stream" or "tutor_respond" or "tutor_stream" or "tutor_evaluate")
                _buffer.RecordChatLatencyMs(measurement);
            else if (op is "embeddings")
                _buffer.RecordEmbeddingLatencyMs(measurement);
        }
        else if (instrument.Name == "knowledge_embedding_latency")
        {
            _buffer.RecordEmbeddingLatencyMs(measurement);
        }
    }

    private static string? GetTagValue(ReadOnlySpan<KeyValuePair<string, object?>> tags, string key)
    {
        foreach (var t in tags)
            if (t.Key == key && t.Value is string s)
                return s;
        return null;
    }

    private async Task CollectAndPersistAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var snapshotRepo = scope.ServiceProvider.GetRequiredService<IOptimizationSnapshotRepository>();
        var tokenRepo = scope.ServiceProvider.GetRequiredService<StudyPilot.Infrastructure.Persistence.Repositories.IKnowledgeTokenUsageRepository>();

        var pipelineSnapshot = _coordinator.GetSnapshot();
        var (avgChat, p95Chat, avgEmbed, retrievalHitRate, retryRate, tokenUsagePerMinute, successRate) = _buffer.GetAndReset();

        long tokenSum = 0;
        try
        {
            tokenSum = await tokenRepo.GetSumSinceAsync(DateTime.UtcNow.AddSeconds(-IntervalSeconds), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Token sum query failed for optimization snapshot");
        }

        var queueDepth = pipelineSnapshot.EmbeddingQueueDepth + pipelineSnapshot.OutboxPendingCount;
        var dto = new OptimizationSnapshotDto(
            CapturedAtUtc: DateTime.UtcNow,
            AvgChatLatencyMs: avgChat,
            P95ChatLatencyMs: p95Chat,
            EmbeddingLatencyMs: avgEmbed,
            RetrievalHitRate: retrievalHitRate,
            RetryRate: retryRate,
            QueueDepth: queueDepth,
            AILimiterWaiters: pipelineSnapshot.AILimiterWaiters,
            TokenUsagePerMinute: tokenSum,
            SuccessRate: successRate);

        try
        {
            await snapshotRepo.InsertAsync(dto, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist optimization snapshot");
        }
    }
}
