namespace StudyPilot.API.Contracts.Requests;

public sealed record SubmitQuizRequest(Guid QuizId, IReadOnlyList<QuizAnswerRequest> Answers);

/// <summary>One answer per question. Send either SubmittedAnswer (option text) or SubmittedOptionIndex (0-based) for reliable grading.</summary>
public sealed record QuizAnswerRequest(Guid QuestionId, string? SubmittedAnswer = null, int? SubmittedOptionIndex = null);
