namespace StudyPilot.Application.Abstractions.Optimization;

/// <summary>
/// Runs the closed-loop optimization cycle: analyze recent snapshots, apply one safe adjustment per cycle, persist and verify.
/// </summary>
public interface IAutonomousOptimizer
{
    Task RunCycleAsync(CancellationToken cancellationToken = default);
}
