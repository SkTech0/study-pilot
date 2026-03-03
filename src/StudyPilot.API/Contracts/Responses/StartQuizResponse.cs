namespace StudyPilot.API.Contracts.Responses;

public sealed record QuizQuestionResponse(Guid Id, string Text, IReadOnlyList<string> Options);
public sealed record StartQuizResponse(Guid QuizId, int TotalQuestionCount, IReadOnlyList<QuizQuestionResponse> Questions);

/// <summary>Per-question response for lazy-loaded quiz; status indicates whether content is ready, still generating, or failed. JobId returned when 202 Accepted.</summary>
public sealed record GetQuizQuestionResponse(Guid Id, string? Text, IReadOnlyList<string>? Options, string Status, string? ErrorMessage = null, Guid? JobId = null);
