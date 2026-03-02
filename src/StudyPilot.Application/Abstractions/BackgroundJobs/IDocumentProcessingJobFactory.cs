namespace StudyPilot.Application.Abstractions.BackgroundJobs;

public interface IDocumentProcessingJobFactory
{
    Func<CancellationToken, Task> CreateProcessDocumentJob(Guid documentId, string? correlationId = null);
}