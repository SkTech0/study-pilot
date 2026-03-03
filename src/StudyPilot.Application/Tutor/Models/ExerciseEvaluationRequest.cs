namespace StudyPilot.Application.Tutor.Models;

public sealed record ExerciseEvaluationRequest(
    Guid ExerciseId,
    string Question,
    string ExpectedAnswer,
    string UserAnswer);
