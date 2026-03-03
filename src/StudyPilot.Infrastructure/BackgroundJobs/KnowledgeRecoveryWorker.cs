using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StudyPilot.Application.Abstractions.Knowledge;
using StudyPilot.Application.Abstractions.AI;
using StudyPilot.Domain.Enums;
using StudyPilot.Infrastructure.AI;
using StudyPilot.Infrastructure.Persistence;
using StudyPilot.Infrastructure.Persistence.DbContext;
using StudyPilot.Infrastructure.Persistence.Repositories;
using StudyPilot.Infrastructure.Services;

namespace StudyPilot.Infrastructure.BackgroundJobs;

/// <summary>
/// Periodically scans for knowledge pipeline inconsistencies and repairs them.
/// Case A: Completed + no chunks -> ensure outbox entry, PendingEmbedding.
/// Case B: Chunks exist but status != Ready -> normalize to Ready.
/// Case C: Stuck in Embedding beyond timeout -> retry (PendingEmbedding).
/// Case D: Outbox missing for document that needs embedding, or outbox stuck in Processing -> create/reset.
/// </summary>
public sealed class KnowledgeRecoveryWorker : BackgroundService
{
    private const string EventTypeDocumentConceptsExtracted = "DocumentConceptsExtracted";
    private static readonly TimeSpan EmbeddingStuckThreshold = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan OutboxProcessingStuckThreshold = TimeSpan.FromMinutes(10);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<KnowledgeRecoveryWorker> _logger;
    private readonly BackgroundJobOptions _options;
    private readonly IOptions<AIServiceOptions> _aiOptions;
    private readonly IKnowledgePipelineCoordinator _coordinator;

    public KnowledgeRecoveryWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<BackgroundJobOptions> options,
        IOptions<AIServiceOptions> aiOptions,
        IKnowledgePipelineCoordinator coordinator,
        ILogger<KnowledgeRecoveryWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
        _aiOptions = aiOptions;
        _coordinator = coordinator;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(30, _options.PollIntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<StudyPilotDbContext>();
                var stateMachine = scope.ServiceProvider.GetRequiredService<IKnowledgeStateMachine>();
                var outboxRepo = scope.ServiceProvider.GetRequiredService<IKnowledgeOutboxRepository>();
                var embeddingRepo = scope.ServiceProvider.GetRequiredService<IKnowledgeEmbeddingJobRepository>();
                var limiter = scope.ServiceProvider.GetRequiredService<IAIExecutionLimiter>();

                var now = DateTime.UtcNow;
                _coordinator.UpdateLoad(
                    await outboxRepo.GetPendingCountAsync(now, stoppingToken),
                    await embeddingRepo.GetPendingCountAsync(stoppingToken),
                    limiter.CurrentConcurrency,
                    limiter.WaitersCount);

                var repaired = 0;
                var prioritizeReadyOnly = _coordinator.ShouldRecoveryPrioritizeReadyOnly();
                var pauseStaleRefresh = _coordinator.ShouldRecoveryPauseStaleRefresh();
                var allowEnqueue = _coordinator.AllowRecoveryEnqueue();

                // Case C: Stuck in Embedding beyond timeout -> retry (always run; completes READY path)
                var stuckEmbedding = await db.Documents
                    .Where(d =>
                        d.KnowledgeStatus == KnowledgeStatus.Embedding &&
                        d.UpdatedAtUtc < now - EmbeddingStuckThreshold)
                    .OrderBy(d => d.UpdatedAtUtc)
                    .Take(50)
                    .ToListAsync(stoppingToken);
                foreach (var doc in stuckEmbedding)
                {
                    if (stoppingToken.IsCancellationRequested) break;
                    try
                    {
                        stateMachine.TransitionToPendingEmbedding(doc);
                        repaired++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Recovery TransitionToPendingEmbedding failed DocumentId={DocumentId}", doc.Id);
                    }
                }

                // Case D: Outbox entries stuck in Processing -> reset to Pending
                var stuckOutbox = await db.KnowledgeOutboxEntries
                    .Where(x =>
                        x.Status == "Processing" &&
                        x.CreatedUtc < now - OutboxProcessingStuckThreshold)
                    .Take(100)
                    .ToListAsync(stoppingToken);
                foreach (var entry in stuckOutbox)
                {
                    if (stoppingToken.IsCancellationRequested) break;
                    entry.Status = "Pending";
                    repaired++;
                }

                // Case A + B: Completed documents not Ready (when not prioritizeReadyOnly, also enqueue; when prioritizeReadyOnly, only fix Ready)
                var candidates = await db.Documents
                    .Where(d =>
                        d.ProcessingStatus == ProcessingStatus.Completed &&
                        d.KnowledgeStatus != KnowledgeStatus.Ready)
                    .OrderBy(d => d.CreatedAtUtc)
                    .Take(100)
                    .ToListAsync(stoppingToken);

                foreach (var doc in candidates)
                {
                    if (stoppingToken.IsCancellationRequested) break;

                    var hasChunks = await db.DocumentChunks.AnyAsync(c => c.DocumentId == doc.Id, stoppingToken);
                    if (hasChunks)
                    {
                        if (doc.KnowledgeStatus != KnowledgeStatus.Ready)
                        {
                            try
                            {
                                stateMachine.TransitionToReady(doc);
                                repaired++;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Recovery TransitionToReady failed DocumentId={DocumentId}", doc.Id);
                            }
                        }
                        continue;
                    }

                    if (prioritizeReadyOnly || !allowEnqueue)
                        continue;

                    var existingOutbox = await db.KnowledgeOutboxEntries
                        .AnyAsync(x =>
                            x.AggregateId == doc.Id &&
                            x.EventType == EventTypeDocumentConceptsExtracted &&
                            (x.Status == "Pending" || x.Status == "Processing" || x.Status == "Completed"),
                            stoppingToken);
                    if (existingOutbox)
                        continue;

                    var outboxEntry = new KnowledgeOutboxEntry
                    {
                        Id = Guid.NewGuid(),
                        AggregateId = doc.Id,
                        EventType = EventTypeDocumentConceptsExtracted,
                        Payload = JsonSerializer.Serialize(new { DocumentId = doc.Id, CorrelationId = (string?)null, Priority = (int)StudyPilot.Application.Abstractions.Knowledge.PipelinePriority.Critical }),
                        Status = "Pending",
                        RetryCount = 0,
                        NextAttemptUtc = null,
                        CreatedUtc = DateTime.UtcNow
                    };
                    await db.KnowledgeOutboxEntries.AddAsync(outboxEntry, stoppingToken);
                    try
                    {
                        stateMachine.TransitionToPendingEmbedding(doc);
                        repaired++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Recovery TransitionToPendingEmbedding failed DocumentId={DocumentId}", doc.Id);
                    }
                }

                // Stale documents: ensure outbox for re-embedding (skip when pauseStaleRefresh or !allowEnqueue)
                if (pauseStaleRefresh || !allowEnqueue)
                {
                    if (repaired > 0)
                    {
                        _coordinator.RecordRecoveryActions(repaired);
                        await db.SaveChangesAsync(stoppingToken);
                        StudyPilotMetrics.KnowledgeRecoveryActions.Add(repaired);
                        StudyPilotMetrics.KnowledgeRecoveryRepairsTotal.Add(repaired);
                        _logger.LogInformation("KnowledgeRecovery repaired {Count} item(s). instance_id={InstanceId} global_mode={GlobalMode} local_mode={LocalMode} CorrelationId=Recovery", repaired, _coordinator.InstanceId, _coordinator.GlobalMode, _coordinator.LocalMode);
                    }
                    try { await Task.Delay(interval, stoppingToken); } catch (OperationCanceledException) { break; }
                    continue;
                }

                var staleDocs = await db.Documents
                    .Where(d => d.KnowledgeStatus == KnowledgeStatus.Stale)
                    .OrderBy(d => d.UpdatedAtUtc)
                    .Take(20)
                    .ToListAsync(stoppingToken);
                foreach (var doc in staleDocs)
                {
                    if (stoppingToken.IsCancellationRequested) break;
                    var hasOutbox = await db.KnowledgeOutboxEntries
                        .AnyAsync(x =>
                            x.AggregateId == doc.Id &&
                            x.EventType == EventTypeDocumentConceptsExtracted &&
                            (x.Status == "Pending" || x.Status == "Processing"),
                            stoppingToken);
                    if (hasOutbox) continue;
                    var entry = new KnowledgeOutboxEntry
                    {
                        Id = Guid.NewGuid(),
                        AggregateId = doc.Id,
                        EventType = EventTypeDocumentConceptsExtracted,
                        Payload = JsonSerializer.Serialize(new { DocumentId = doc.Id, CorrelationId = (string?)null, Priority = (int)StudyPilot.Application.Abstractions.Knowledge.PipelinePriority.Low }),
                        Status = "Pending",
                        RetryCount = 0,
                        NextAttemptUtc = null,
                        CreatedUtc = DateTime.UtcNow
                    };
                    await db.KnowledgeOutboxEntries.AddAsync(entry, stoppingToken);
                    try
                    {
                        stateMachine.TransitionToPendingEmbedding(doc);
                        repaired++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Recovery Stale TransitionToPendingEmbedding failed DocumentId={DocumentId}", doc.Id);
                    }
                }

                // Freshness: Ready documents whose chunks have outdated embedding/chunking version -> Stale
                var opts = _aiOptions.Value;
                var currentEmbeddingVersion = Math.Max(1, opts.EmbeddingVersion);
                var currentChunkingVersion = Math.Max(1, opts.ChunkingVersion);
                var currentModel = opts.EmbeddingModelId ?? "default";
                var staleDocumentIds = await db.DocumentChunks
                    .Where(c => c.EmbeddingVersion < currentEmbeddingVersion ||
                                c.ChunkingVersion < currentChunkingVersion ||
                                (c.EmbeddingModel != null && c.EmbeddingModel != currentModel))
                    .Select(c => c.DocumentId)
                    .Distinct()
                    .Take(50)
                    .ToListAsync(stoppingToken);
                var readyStaleIds = await db.Documents
                    .Where(d => d.KnowledgeStatus == KnowledgeStatus.Ready && staleDocumentIds.Contains(d.Id))
                    .Select(d => d.Id)
                    .Take(20)
                    .ToListAsync(stoppingToken);
                var readyWithStaleChunks = await db.Documents
                    .Where(d => readyStaleIds.Contains(d.Id))
                    .ToListAsync(stoppingToken);
                foreach (var doc in readyWithStaleChunks)
                {
                    if (stoppingToken.IsCancellationRequested) break;
                    try
                    {
                        stateMachine.TransitionToStale(doc);
                        repaired++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Recovery TransitionToStale failed DocumentId={DocumentId}", doc.Id);
                    }
                }

                if (repaired > 0)
                {
                    _coordinator.RecordRecoveryActions(repaired);
                    await db.SaveChangesAsync(stoppingToken);
                    StudyPilotMetrics.KnowledgeRecoveryActions.Add(repaired);
                    StudyPilotMetrics.KnowledgeRecoveryRepairsTotal.Add(repaired);
                    _logger.LogInformation("KnowledgeRecovery repaired {Count} item(s). instance_id={InstanceId} global_mode={GlobalMode} local_mode={LocalMode} CorrelationId=Recovery", repaired, _coordinator.InstanceId, _coordinator.GlobalMode, _coordinator.LocalMode);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "KnowledgeRecoveryWorker loop error");
                StudyPilotMetrics.BackgroundJobFailuresTotal.Add(1);
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
