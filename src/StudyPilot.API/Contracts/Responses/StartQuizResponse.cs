namespace StudyPilot.API.Contracts.Responses;

public sealed record QuizQuestionResponse(Guid Id, string Text, IReadOnlyList<string> Options);
public sealed record StartQuizResponse(Guid QuizId, IReadOnlyList<QuizQuestionResponse> Questions);
