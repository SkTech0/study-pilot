namespace StudyPilot.Application.Abstractions.BackgroundJobs;

public interface IKnowledgeEmbeddingJobFactory
{
    Func<CancellationToken, Task> CreateEmbeddingJob(Guid documentId, string? correlationId = null);
}

