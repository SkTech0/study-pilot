namespace StudyPilot.Application.Common.Models;

public sealed record GenerateQuizResult(IReadOnlyList<GeneratedQuestion> Questions);
