using StudyPilot.Application.Tutor.Models;

namespace StudyPilot.Application.Abstractions.Tutor;

public interface ITutorService
{
    Task<TutorResponse> RespondAsync(TutorContext context, CancellationToken cancellationToken = default);
    Task<TutorStreamResult> StreamRespondAsync(TutorContext context, Func<string, Task> onToken, CancellationToken cancellationToken = default);
    Task<ExerciseEvaluationResult> EvaluateExerciseAsync(ExerciseEvaluationRequest request, CancellationToken cancellationToken = default);
}
