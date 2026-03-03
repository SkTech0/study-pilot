using MediatR;
using StudyPilot.Application.Common.Models;

namespace StudyPilot.Application.Tutor.TutorRespond;

public sealed record TutorRespondCommand(Guid UserId, Guid SessionId, string Message) : IRequest<Result<TutorRespondResult>>;

public sealed record TutorRespondResult(
    string AssistantMessage,
    string NextStep,
    TutorExerciseResult? OptionalExercise,
    IReadOnlyList<Guid> CitedChunkIds);

public sealed record TutorExerciseResult(Guid ExerciseId, string Question, string ExpectedAnswer, string Difficulty);
