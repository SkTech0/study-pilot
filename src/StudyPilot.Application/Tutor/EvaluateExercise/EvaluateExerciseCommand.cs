using MediatR;
using StudyPilot.Application.Common.Models;

namespace StudyPilot.Application.Tutor.EvaluateExercise;

public sealed record EvaluateExerciseCommand(
    Guid UserId,
    Guid ExerciseId,
    string UserAnswer) : IRequest<Result<EvaluateExerciseResult>>;

public sealed record EvaluateExerciseResult(bool IsCorrect, string Explanation);
