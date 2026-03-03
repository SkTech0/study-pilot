namespace StudyPilot.Application.Abstractions.Optimization;

public interface IOptimizationConfigRepository
{
    Task<OptimizationConfigDto?> GetSingleAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(OptimizationConfigDto config, CancellationToken cancellationToken = default);
    Task SaveWithHistoryAsync(OptimizationConfigDto config, CancellationToken cancellationToken = default);
    Task<OptimizationConfigDto?> GetPreviousVersionAsync(CancellationToken cancellationToken = default);
}
