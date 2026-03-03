using StudyPilot.Application.Abstractions.BackgroundJobs;
using StudyPilot.Application.Abstractions.Observability;
using StudyPilot.Application.Abstractions.Persistence;
using StudyPilot.Application.Common.Errors;
using StudyPilot.Application.Common.Models;
using MediatR;
using StudyPilot.Domain.Entities;
using StudyPilot.Domain.Enums;

namespace StudyPilot.Application.Quiz.GetQuizQuestion;

public sealed class GetQuizQuestionQueryHandler : IRequestHandler<GetQuizQuestionQuery, Result<GetQuizQuestionResult>>
{
    private readonly IQuizRepository _quizRepository;
    private readonly IQuizQuestionGenerationJobQueue _quizJobQueue;
    private readonly ICorrelationIdAccessor? _correlationIdAccessor;

    public GetQuizQuestionQueryHandler(
        IQuizRepository quizRepository,
        IQuizQuestionGenerationJobQueue quizJobQueue,
        ICorrelationIdAccessor? correlationIdAccessor = null)
    {
        _quizRepository = quizRepository;
        _quizJobQueue = quizJobQueue;
        _correlationIdAccessor = correlationIdAccessor;
    }

    public async Task<Result<GetQuizQuestionResult>> Handle(GetQuizQuestionQuery request, CancellationToken cancellationToken)
    {
        var quiz = await _quizRepository.GetByIdAsync(request.QuizId, cancellationToken);
        if (quiz is null)
            return Result<GetQuizQuestionResult>.Failure(new AppError(ErrorCodes.QuizNotFound, "Quiz not found.", null, ErrorSeverity.Business));
        if (request.QuestionIndex < 0 || request.QuestionIndex >= quiz.TotalQuestionCount)
            return Result<GetQuizQuestionResult>.Failure(new AppError(ErrorCodes.QuizQuestionIndexOutOfRange, "Question index out of range.", null, ErrorSeverity.Business));

        var question = await _quizRepository.GetQuestionByQuizAndIndexAsync(request.QuizId, request.QuestionIndex, cancellationToken);
        var weInserted = false;

        if (question is null)
        {
            var placeholder = Question.CreatePlaceholder(request.QuizId, request.QuestionIndex);
            weInserted = await _quizRepository.TryAddQuestionAsync(placeholder, cancellationToken);
            if (weInserted)
                question = placeholder;
            else
                question = await _quizRepository.GetQuestionByQuizAndIndexAsync(request.QuizId, request.QuestionIndex, cancellationToken);
        }

        if (question is null)
            return Result<GetQuizQuestionResult>.Failure(new AppError(ErrorCodes.UnexpectedError, "Could not create or load question slot.", null, ErrorSeverity.System));

        if (weInserted)
        {
            var jobId = await _quizJobQueue.EnqueueAsync(request.QuizId, request.QuestionIndex, _correlationIdAccessor?.Get(), cancellationToken);
            return Result<GetQuizQuestionResult>.Success(new GetQuizQuestionResult(question.Id, null, null, QuestionGenerationStatus.Generating, null, jobId));
        }

        if (question!.Status == QuestionGenerationStatus.Generating)
            return Result<GetQuizQuestionResult>.Success(new GetQuizQuestionResult(question.Id, null, null, QuestionGenerationStatus.Generating, null, null));

        if (question.Status == QuestionGenerationStatus.Failed)
            return Result<GetQuizQuestionResult>.Success(new GetQuizQuestionResult(question.Id, null, null, QuestionGenerationStatus.Failed, question.ErrorMessage, null));

        if (request.QuestionIndex + 1 < quiz.TotalQuestionCount)
            _ = _quizJobQueue.EnqueueAsync(request.QuizId, request.QuestionIndex + 1, _correlationIdAccessor?.Get(), CancellationToken.None);

        return Result<GetQuizQuestionResult>.Success(new GetQuizQuestionResult(
            question.Id,
            question.Text,
            question.Options.ToList(),
            QuestionGenerationStatus.Ready,
            null,
            null));
    }
}
