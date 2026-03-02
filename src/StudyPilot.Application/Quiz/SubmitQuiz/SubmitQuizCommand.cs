using MediatR;
using StudyPilot.Application.Common.Models;

namespace StudyPilot.Application.Quiz.SubmitQuiz;

public sealed record QuizAnswerInput(Guid QuestionId, string SubmittedAnswer);

public sealed record SubmitQuizCommand(Guid QuizId, Guid UserId, IReadOnlyList<QuizAnswerInput> Answers) : IRequest<Result<SubmitQuizResult>>;

public sealed record SubmitQuizResult(int CorrectCount, int TotalCount);
