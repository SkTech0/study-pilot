namespace StudyPilot.Application.Abstractions.BackgroundJobs;

public interface IQuizQuestionGenerationJobQueue
{
    Task<Guid> EnqueueAsync(Guid quizId, int questionIndex, string? correlationId, CancellationToken cancellationToken = default);
}
