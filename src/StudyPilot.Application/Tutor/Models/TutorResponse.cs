using StudyPilot.Domain.Enums;

namespace StudyPilot.Application.Tutor.Models;

public sealed record TutorResponse(
    string Message,
    TutorStep NextStep,
    TutorExerciseInfo? OptionalExercise,
    IReadOnlyList<Guid> CitedChunkIds);

public sealed record TutorExerciseInfo(string Question, string ExpectedAnswer, string Difficulty);
