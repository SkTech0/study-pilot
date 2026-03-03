namespace StudyPilot.Application.Abstractions.Optimization;

public interface IOptimizationSnapshotRepository
{
    Task InsertAsync(OptimizationSnapshotDto snapshot, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OptimizationSnapshotDto>> GetLastNAsync(int n, CancellationToken cancellationToken = default);
}
