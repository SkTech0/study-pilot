using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StudyPilot.Application.Abstractions.AI;
using StudyPilot.Application.Abstractions.BackgroundJobs;
using StudyPilot.Application.Abstractions.Knowledge;
using StudyPilot.Infrastructure.Persistence;
using StudyPilot.Infrastructure.Persistence.Repositories;
using StudyPilot.Infrastructure.Services;

namespace StudyPilot.Infrastructure.BackgroundJobs;

/// <summary>
/// Dispatches knowledge outbox events to the embedding job queue.
/// Consults coordinator for load shedding; uses classifier for retry decisions.
/// </summary>
public sealed class KnowledgeOutboxDispatcher : BackgroundService
{
    private const string EventTypeDocumentConceptsExtracted = "DocumentConceptsExtracted";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<KnowledgeOutboxDispatcher> _logger;
    private readonly BackgroundJobOptions _options;
    private readonly IKnowledgePipelineCoordinator _coordinator;
    private readonly IAIFailureClassifier _classifier;

    public KnowledgeOutboxDispatcher(
        IServiceScopeFactory scopeFactory,
        IOptions<BackgroundJobOptions> options,
        IKnowledgePipelineCoordinator coordinator,
        IAIFailureClassifier classifier,
        ILogger<KnowledgeOutboxDispatcher> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
        _coordinator = coordinator;
        _classifier = classifier;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var basePollInterval = TimeSpan.FromSeconds(Math.Max(2, _options.PollIntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var outboxRepo = scope.ServiceProvider.GetRequiredService<IKnowledgeOutboxRepository>();
                var embeddingQueue = scope.ServiceProvider.GetRequiredService<IKnowledgeEmbeddingJobQueue>();
                var embeddingRepo = scope.ServiceProvider.GetRequiredService<IKnowledgeEmbeddingJobRepository>();
                var limiter = scope.ServiceProvider.GetRequiredService<IAIExecutionLimiter>();

                var now = DateTime.UtcNow;
                _coordinator.UpdateLoad(
                    await outboxRepo.GetPendingCountAsync(now, stoppingToken),
                    await embeddingRepo.GetPendingCountAsync(stoppingToken),
                    limiter.CurrentConcurrency,
                    limiter.WaitersCount);

                if (!_coordinator.AllowEnrichmentScheduling())
                {
                    _logger.LogWarning("KnowledgeOutboxDispatchSkipped instance_id={InstanceId} global_mode={GlobalMode} pipeline overloaded", _coordinator.InstanceId, _coordinator.GlobalMode);
                    await Task.Delay(_coordinator.GetOutboxDispatchPollDelay(basePollInterval), stoppingToken);
                    continue;
                }

                var batchSize = _coordinator.GetOutboxDispatchBatchSize();
                var batch = await outboxRepo.GetPendingBatchAsync(maxItems: batchSize, now, stoppingToken);
                if (batch.Count == 0)
                {
                    await Task.Delay(_coordinator.GetOutboxDispatchPollDelay(basePollInterval), stoppingToken);
                    continue;
                }

                foreach (var entry in batch)
                {
                    if (stoppingToken.IsCancellationRequested)
                        break;

                    try
                    {
                        await outboxRepo.MarkProcessingAsync(entry.Id, stoppingToken);

                        if (string.Equals(entry.EventType, EventTypeDocumentConceptsExtracted, StringComparison.Ordinal))
                        {
                            var payload = JsonSerializer.Deserialize<DocumentConceptsExtractedPayload>(entry.Payload);
                            if (payload == null || payload.DocumentId == Guid.Empty)
                            {
                                await outboxRepo.MarkFailedAsync(entry.Id, allowRetry: false, nextAttemptUtc: null, stoppingToken);
                                continue;
                            }

                            var priority = payload.Priority is >= 0 and <= 3
                                ? (StudyPilot.Application.Abstractions.Knowledge.PipelinePriority)payload.Priority.Value
                                : StudyPilot.Application.Abstractions.Knowledge.PipelinePriority.High;
                            await embeddingQueue.EnqueueCreateEmbeddingsAsync(payload.DocumentId, payload.CorrelationId, priority, stoppingToken);
                        }

                        await outboxRepo.MarkProcessedAsync(entry.Id, stoppingToken);
                        StudyPilotMetrics.OutboxDispatchSuccess.Add(1);
                    }
                    catch (Exception ex)
                    {
                        var classification = _classifier.Classify(ex, entry.RetryCount, _options.MaxRetries);
                        var allowRetry = classification.AllowRetry;
                        var backoffSeconds = classification.RetryDelaySeconds;
                        var nextAttempt = allowRetry ? DateTime.UtcNow.AddSeconds(backoffSeconds) : (DateTime?)null;
                        if (classification.OpenCircuit)
                            limiter.SetCircuitOpen(true);
                        await outboxRepo.MarkFailedAsync(entry.Id, allowRetry, nextAttempt, stoppingToken);
                        StudyPilotMetrics.KnowledgeOutboxRetryTotal.Add(1);
                        if (allowRetry)
                            StudyPilotMetrics.JobRetriesTotal.Add(1, new KeyValuePair<string, object?>("queue", "knowledge_outbox"));
                        _logger.LogError(ex, "KnowledgeOutboxDispatchFailed OutboxId={OutboxId} EventType={EventType} RetryCount={RetryCount} Kind={Kind} instance_id={InstanceId} global_mode={GlobalMode} local_mode={LocalMode}",
                            entry.Id, entry.EventType, entry.RetryCount, classification.Kind, _coordinator.InstanceId, _coordinator.GlobalMode, _coordinator.LocalMode);
                    }
                }

                await Task.Delay(_coordinator.GetOutboxDispatchPollDelay(basePollInterval), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "KnowledgeOutboxDispatcher loop error instance_id={InstanceId} global_mode={GlobalMode} local_mode={LocalMode}", _coordinator.InstanceId, _coordinator.GlobalMode, _coordinator.LocalMode);
                StudyPilotMetrics.BackgroundJobFailuresTotal.Add(1);
                await Task.Delay(basePollInterval, stoppingToken);
            }
        }
    }

    private sealed class DocumentConceptsExtractedPayload
    {
        public Guid DocumentId { get; set; }
        public string? CorrelationId { get; set; }
        public int? Priority { get; set; }
    }
}

