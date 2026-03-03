using StudyPilot.Infrastructure.Persistence;

namespace StudyPilot.Infrastructure.Persistence.Repositories;

public interface IKnowledgePipelineHeartbeatRepository
{
    Task UpsertAsync(KnowledgePipelineHeartbeat heartbeat, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<KnowledgePipelineHeartbeat>> GetActiveHeartbeatsAsync(TimeSpan withinLast, CancellationToken cancellationToken = default);
}
