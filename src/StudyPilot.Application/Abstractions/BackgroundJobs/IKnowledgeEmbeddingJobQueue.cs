using StudyPilot.Application.Abstractions.Knowledge;

namespace StudyPilot.Application.Abstractions.BackgroundJobs;

public interface IKnowledgeEmbeddingJobQueue
{
    Task EnqueueCreateEmbeddingsAsync(Guid documentId, string? correlationId, PipelinePriority priority = PipelinePriority.High, CancellationToken cancellationToken = default);
}

