using StudyPilot.Domain.Enums;

namespace StudyPilot.Application.Tutor.Models;

public sealed record TutorStreamResult(
    TutorStep NextStep,
    TutorExerciseInfo? OptionalExercise,
    IReadOnlyList<Guid> CitedChunkIds);
