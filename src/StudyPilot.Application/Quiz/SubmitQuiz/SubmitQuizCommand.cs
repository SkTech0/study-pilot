using MediatR;
using StudyPilot.Application.Common.Models;

namespace StudyPilot.Application.Quiz.SubmitQuiz;

public sealed record QuizAnswerInput(Guid QuestionId, string? SubmittedAnswer, int? SubmittedOptionIndex);

public sealed record SubmitQuizCommand(Guid QuizId, Guid UserId, IReadOnlyList<QuizAnswerInput> Answers) : IRequest<Result<SubmitQuizResult>>;

public sealed record SubmitQuizResult(int CorrectCount, int TotalCount, IReadOnlyList<QuestionResultItem>? QuestionResults = null);

public sealed record QuestionResultItem(Guid QuestionId, bool IsCorrect, string CorrectAnswer, int CorrectOptionIndex);
