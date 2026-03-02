using MediatR;
using StudyPilot.Application.Common.Models;

namespace StudyPilot.Application.Quiz.StartQuiz;

public sealed record StartQuizCommand(Guid DocumentId, Guid UserId) : IRequest<Result<StartQuizResult>>;

public sealed record StartQuizQuestionSummary(Guid Id, string Text, IReadOnlyList<string> Options);
public sealed record StartQuizResult(Guid QuizId, int TotalQuestionCount, IReadOnlyList<StartQuizQuestionSummary> Questions);
