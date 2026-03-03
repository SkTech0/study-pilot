using MediatR;
using StudyPilot.Application.Common.Models;
using StudyPilot.Domain.Enums;

namespace StudyPilot.Application.Quiz.GetQuizQuestion;

public sealed record GetQuizQuestionQuery(Guid QuizId, int QuestionIndex) : IRequest<Result<GetQuizQuestionResult>>;

public sealed record GetQuizQuestionResult(
    Guid Id,
    string? Text,
    IReadOnlyList<string>? Options,
    QuestionGenerationStatus Status,
    string? ErrorMessage = null,
    Guid? JobId = null);
