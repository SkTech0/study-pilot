namespace StudyPilot.Application.Abstractions.Optimization;

/// <summary>
/// Decides whether the autonomous optimizer is allowed to run. When frozen, optimizer skips the cycle.
/// </summary>
public interface IOptimizationSafetyGuard
{
    bool ShouldFreezeOptimization();
}
