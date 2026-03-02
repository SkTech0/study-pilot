using Microsoft.Extensions.DependencyInjection;
using StudyPilot.Application.Abstractions.AI;
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
    private const int PreloadTimeoutSeconds = 90;
    private readonly IQuizRepository _quizRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IQuestionGenerationDispatcher _dispatcher;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ICorrelationIdAccessor? _correlationIdAccessor;

    public GetQuizQuestionQueryHandler(
        IQuizRepository quizRepository,
        IUnitOfWork unitOfWork,
        IQuestionGenerationDispatcher dispatcher,
        IServiceScopeFactory scopeFactory,
        ICorrelationIdAccessor? correlationIdAccessor = null)
    {
        _quizRepository = quizRepository;
        _unitOfWork = unitOfWork;
        _dispatcher = dispatcher;
        _scopeFactory = scopeFactory;
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
            await _dispatcher.DispatchAsync(request.QuizId, request.QuestionIndex, cancellationToken);
            question = await _quizRepository.GetQuestionByQuizAndIndexAsync(request.QuizId, request.QuestionIndex, cancellationToken);
        }

        if (question!.Status == QuestionGenerationStatus.Generating)
        {
            var loading = new GetQuizQuestionResult(question.Id, null, null, QuestionGenerationStatus.Generating, null);
            return Result<GetQuizQuestionResult>.Success(loading);
        }

        if (question.Status == QuestionGenerationStatus.Failed)
        {
            var failed = new GetQuizQuestionResult(question.Id, null, null, QuestionGenerationStatus.Failed, question.ErrorMessage);
            return Result<GetQuizQuestionResult>.Success(failed);
        }

        var ready = new GetQuizQuestionResult(
            question.Id,
            question.Text,
            question.Options.ToList(),
            QuestionGenerationStatus.Ready,
            null);
        var result = Result<GetQuizQuestionResult>.Success(ready);

        if (request.QuestionIndex + 1 < quiz.TotalQuestionCount)
        {
            var quizId = request.QuizId;
            var nextIndex = request.QuestionIndex + 1;
            var correlationId = _correlationIdAccessor?.Get();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(PreloadTimeoutSeconds));
            _ = Task.Run(async () =>
            {
                try
                {
                    await using var scope = _scopeFactory.CreateAsyncScope();
                    var accessor = scope.ServiceProvider.GetService<ICorrelationIdAccessor>();
                    if (!string.IsNullOrEmpty(correlationId))
                        accessor?.Set(correlationId);
                    var disp = scope.ServiceProvider.GetRequiredService<IQuestionGenerationDispatcher>();
                    await disp.DispatchAsync(quizId, nextIndex, cts.Token);
                }
                catch (OperationCanceledException) { }
                catch { /* Preload best-effort; avoid unobserved task exceptions */ }
            }, cts.Token);
        }

        return result;
    }
}
