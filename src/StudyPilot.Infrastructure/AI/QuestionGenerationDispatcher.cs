using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StudyPilot.Application.Abstractions.AI;
using StudyPilot.Application.Abstractions.Persistence;
using StudyPilot.Application.Common.Models;
using StudyPilot.Domain.Entities;
using StudyPilot.Domain.Enums;

namespace StudyPilot.Infrastructure.AI;

public sealed class QuestionGenerationDispatcher : IQuestionGenerationDispatcher
{
    private const int MaxRetries = 3;
    private static readonly ConcurrentDictionary<(Guid QuizId, int QuestionIndex), SemaphoreSlim> SlotLocks = new();

    private readonly IQuizRepository _quizRepository;
    private readonly IConceptRepository _conceptRepository;
    private readonly IQuestionConceptLinkRepository _questionConceptLinkRepository;
    private readonly IAIService _aiService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly int _retryBaseDelayMs;

    public QuestionGenerationDispatcher(
        IQuizRepository quizRepository,
        IConceptRepository conceptRepository,
        IQuestionConceptLinkRepository questionConceptLinkRepository,
        IAIService aiService,
        IUnitOfWork unitOfWork,
        IOptions<AIServiceOptions> aiOptions)
    {
        _quizRepository = quizRepository;
        _conceptRepository = conceptRepository;
        _questionConceptLinkRepository = questionConceptLinkRepository;
        _aiService = aiService;
        _unitOfWork = unitOfWork;
        _retryBaseDelayMs = aiOptions.Value.QuestionGenerationRetryBaseDelayMs is > 0 and <= 30_000
            ? aiOptions.Value.QuestionGenerationRetryBaseDelayMs
            : 500;
    }

    public async Task DispatchAsync(Guid quizId, int questionIndex, CancellationToken cancellationToken = default)
    {
        var key = (quizId, questionIndex);
        var sem = SlotLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await sem.WaitAsync(cancellationToken);
        try
        {
            var question = await _quizRepository.GetQuestionByQuizAndIndexAsync(quizId, questionIndex, cancellationToken);
            if (question is null || question.Status != QuestionGenerationStatus.Generating)
                return;

            var quiz = await _quizRepository.GetByIdAsync(quizId, cancellationToken);
        if (quiz is null || questionIndex >= quiz.TotalQuestionCount)
            return;

        var concepts = await _conceptRepository.GetByDocumentIdAsync(quiz.DocumentId, cancellationToken);
        if (questionIndex >= concepts.Count)
        {
            question.MarkFailed("No concept for question index.");
            await _quizRepository.UpdateQuestionAsync(question, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return;
        }

        var concept = concepts[questionIndex];
        var conceptInfo = new ConceptInfo(concept.Id, concept.Name, concept.Description);

        Exception? lastException = null;
        for (var attempt = 1; attempt <= MaxRetries; attempt++)
        {
            if (attempt > 1)
            {
                question.IncrementAttempts();
                await _quizRepository.UpdateQuestionAsync(question, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            try
            {
                var generated = await _aiService.GenerateQuestionAsync(quiz.DocumentId, quiz.CreatedForUserId, conceptInfo, cancellationToken);
                if (generated is not null && !string.IsNullOrWhiteSpace(generated.CorrectAnswer))
                {
                    question.MarkReady(
                        generated.Text,
                        generated.QuestionType,
                        generated.CorrectAnswer,
                        generated.Options.ToList(),
                        generated.PromptVersion,
                        generated.ModelName);
                    await _quizRepository.UpdateQuestionAsync(question, cancellationToken);
                    await _questionConceptLinkRepository.AddAsync(question.Id, concept.Id, cancellationToken);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                    return;
                }
            }
            catch (Exception ex)
            {
                lastException = ex;
            }

            if (attempt < MaxRetries)
            {
                var delayMs = _retryBaseDelayMs * (1 << (attempt - 1));
                await Task.Delay(delayMs, cancellationToken);
            }
        }

        question.MarkFailed(lastException?.Message ?? "Generation failed after retries.");
        await _quizRepository.UpdateQuestionAsync(question, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            sem.Release();
        }
    }
}
