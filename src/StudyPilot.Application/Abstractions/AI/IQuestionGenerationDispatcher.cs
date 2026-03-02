namespace StudyPilot.Application.Abstractions.AI;

/// <summary>Dispatches question generation for a given quiz slot. Default implementation runs synchronously; can be replaced with a queue-based implementation later.</summary>
public interface IQuestionGenerationDispatcher
{
    Task DispatchAsync(Guid quizId, int questionIndex, CancellationToken cancellationToken = default);
}
