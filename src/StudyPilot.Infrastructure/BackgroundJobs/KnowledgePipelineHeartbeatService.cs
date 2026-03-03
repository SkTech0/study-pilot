using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StudyPilot.Application.Abstractions.Knowledge;
using StudyPilot.Infrastructure.Persistence;
using StudyPilot.Infrastructure.Persistence.Repositories;
using IKnowledgeEmbeddingJobRepository = StudyPilot.Infrastructure.Persistence.Repositories.IKnowledgeEmbeddingJobRepository;

namespace StudyPilot.Infrastructure.BackgroundJobs;

/// <summary>
/// Updates this instance's heartbeat every 10s and computes global mode from active instances (LastSeen within 30s).
/// Global mode = worst(CurrentMode) across active. Coordinator decisions use global mode.
/// </summary>
public sealed class KnowledgePipelineHeartbeatService : BackgroundService
{
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ActiveWithin = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IKnowledgePipelineCoordinator _coordinator;
    private readonly ILogger<KnowledgePipelineHeartbeatService> _logger;

    public KnowledgePipelineHeartbeatService(
        IServiceScopeFactory scopeFactory,
        IKnowledgePipelineCoordinator coordinator,
        ILogger<KnowledgePipelineHeartbeatService> logger)
    {
        _scopeFactory = scopeFactory;
        _coordinator = coordinator;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("KnowledgePipelineHeartbeatService started InstanceId={InstanceId}", _coordinator.InstanceId);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var heartbeatRepo = scope.ServiceProvider.GetRequiredService<IKnowledgePipelineHeartbeatRepository>();
                var tokenUsageRepo = scope.ServiceProvider.GetRequiredService<IKnowledgeTokenUsageRepository>();

                var snapshot = _coordinator.GetSnapshot();
                var heartbeat = new KnowledgePipelineHeartbeat
                {
                    InstanceId = _coordinator.InstanceId,
                    LastSeenUtc = DateTime.UtcNow,
                    CurrentMode = (int)_coordinator.LocalMode,
                    OutboxPending = snapshot.OutboxPendingCount,
                    EmbeddingDepth = snapshot.EmbeddingQueueDepth,
                    AILimiterWaiters = snapshot.AILimiterWaiters
                };
                await heartbeatRepo.UpsertAsync(heartbeat, stoppingToken);

                var active = await heartbeatRepo.GetActiveHeartbeatsAsync(ActiveWithin, stoppingToken);
                var globalMode = ComputeWorstMode(active);
                _coordinator.SetGlobalMode(globalMode);

                var rolling24h = await tokenUsageRepo.GetSumLast24HoursAsync(stoppingToken);
                _coordinator.SetRolling24hTokenUsage(rolling24h);

                var embeddingRepo = scope.ServiceProvider.GetRequiredService<IKnowledgeEmbeddingJobRepository>();
                var priorityDepths = await embeddingRepo.GetPendingCountByPriorityAsync(stoppingToken);
                _coordinator.SetPriorityQueueDepths(priorityDepths);

                await tokenUsageRepo.PruneOlderThanAsync(TimeSpan.FromHours(48), stoppingToken);

                if (active.Count > 1)
                    _logger.LogDebug("KnowledgePipelineHeartbeat InstanceId={InstanceId} LocalMode={LocalMode} GlobalMode={GlobalMode} ActiveInstances={Count}",
                        _coordinator.InstanceId, _coordinator.LocalMode, globalMode, active.Count);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "KnowledgePipelineHeartbeatService error InstanceId={InstanceId} global_mode={GlobalMode} local_mode={LocalMode}",
                    _coordinator.InstanceId, _coordinator.GlobalMode, _coordinator.LocalMode);
            }

            try
            {
                await Task.Delay(HeartbeatInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("KnowledgePipelineHeartbeatService stopped InstanceId={InstanceId}", _coordinator.InstanceId);
    }

    private static PipelineMode ComputeWorstMode(IReadOnlyList<KnowledgePipelineHeartbeat> active)
    {
        if (active.Count == 0) return PipelineMode.Normal;
        var worst = (PipelineMode)active.Max(h => h.CurrentMode);
        return worst;
    }
}
