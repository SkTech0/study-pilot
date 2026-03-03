using MediatR;
using StudyPilot.Application.Common.Models;

namespace StudyPilot.Application.Tutor.StreamTutor;

public sealed record StreamTutorQuery(Guid UserId, Guid SessionId, string Message) : IRequest<Result<StreamTutorResult>>;

public sealed record StreamTutorResult(
    IAsyncEnumerable<string> Tokens,
    Task WhenComplete,
    string NextStep,
    TutorStreamExerciseResult? OptionalExercise,
    IReadOnlyList<Guid> CitedChunkIds);

public sealed record TutorStreamExerciseResult(Guid ExerciseId, string Question, string ExpectedAnswer, string Difficulty);
