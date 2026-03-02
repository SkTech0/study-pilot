namespace StudyPilot.Application.Abstractions.BackgroundJobs;

public interface IKnowledgeEmbeddingJobQueue
{
    Task EnqueueCreateEmbeddingsAsync(Guid documentId, string? correlationId, CancellationToken cancellationToken = default);
}

