namespace StudyPilot.API.Contracts.Requests;

public sealed record SubmitQuizRequest(Guid QuizId, IReadOnlyList<QuizAnswerRequest> Answers);

public sealed record QuizAnswerRequest(Guid QuestionId, string SubmittedAnswer);
