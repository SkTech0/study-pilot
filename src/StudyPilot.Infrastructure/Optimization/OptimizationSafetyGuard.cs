using Microsoft.Extensions.Logging;
using StudyPilot.Application.Abstractions.AI;
using StudyPilot.Application.Abstractions.Knowledge;
using StudyPilot.Application.Abstractions.Optimization;

namespace StudyPilot.Infrastructure.Optimization;

public sealed class OptimizationSafetyGuard : IOptimizationSafetyGuard
{
    private readonly IKnowledgePipelineCoordinator _coordinator;
    private readonly IAIExecutionLimiter _limiter;
    private readonly ILogger<OptimizationSafetyGuard> _logger;
    private int _recoverySpikeThreshold = 10;

    public OptimizationSafetyGuard(
        IKnowledgePipelineCoordinator coordinator,
        IAIExecutionLimiter limiter,
        ILogger<OptimizationSafetyGuard> logger)
    {
        _coordinator = coordinator;
        _limiter = limiter;
        _logger = logger;
    }

    public bool ShouldFreezeOptimization()
    {
        if (_coordinator.GlobalMode == PipelineMode.Overloaded)
        {
            _logger.LogDebug("Optimization frozen: pipeline_mode=Overloaded");
            return true;
        }

        if (!_limiter.IsAvailable)
        {
            _logger.LogDebug("Optimization frozen: AI circuit open");
            return true;
        }

        var snapshot = _coordinator.GetSnapshot();
        if (snapshot.RecoveryActionsRate >= _recoverySpikeThreshold)
        {
            _logger.LogDebug("Optimization frozen: recovery_actions_spike Rate={Rate}", snapshot.RecoveryActionsRate);
            return true;
        }

        return false;
    }
}
