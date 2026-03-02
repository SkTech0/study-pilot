using StudyPilot.Domain.Enums;

namespace StudyPilot.Application.Common.Models;

public sealed record GeneratedQuestion(string Text, QuestionType QuestionType, string CorrectAnswer, IReadOnlyList<string> Options, Guid ConceptId);
