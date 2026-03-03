using System.Collections.Generic;
using StudyPilot.Application.Abstractions.Caching;
using StudyPilot.Application.Abstractions.Learning;
using StudyPilot.Application.Abstractions.Persistence;
using StudyPilot.Application.Common.Errors;
using StudyPilot.Application.Common.Models;
using MediatR;
using StudyPilot.Domain.Entities;

namespace StudyPilot.Application.Quiz.SubmitQuiz;

internal static class QuizGrading
{
    private static string Normalize(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        var trimmed = (s ?? "").Trim();
        var t = string.Join(" ", trimmed.Split([' ', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries)).Trim();
        return t.Length == 0 ? "" : t.Normalize(System.Text.NormalizationForm.FormKC);
    }

    /// <summary>Resolve stored correct answer (option text, or letter A–D, or 1–4) to the exact option text and optionally to 0-based index.</summary>
    public static string ResolveCorrectAnswerText(string correctAnswer, IReadOnlyList<string> options)
    {
        if (options.Count == 0) return Normalize(correctAnswer);
        var raw = Normalize(correctAnswer);
        if (raw.Length == 1 && raw[0] is >= 'A' and <= 'D')
        {
            var idx = raw[0] - 'A';
            if (idx < options.Count) return Normalize(options[idx]);
        }
        if (raw.Length >= 1 && raw.All(char.IsDigit))
        {
            var i = int.TryParse(raw, out var n) ? n : -1;
            if (i >= 1 && i <= options.Count) return Normalize(options[i - 1]);
            if (i >= 0 && i < options.Count) return Normalize(options[i]);
        }
        return raw;
    }

    /// <summary>Get 0-based index of the correct option. Returns -1 if not determinable.</summary>
    public static int ResolveCorrectAnswerIndex(string correctAnswer, IReadOnlyList<string> options)
    {
        if (options.Count == 0) return -1;
        var raw = Normalize(correctAnswer);
        if (raw.Length == 1 && raw[0] is >= 'A' and <= 'D')
        {
            var idx = raw[0] - 'A';
            if (idx < options.Count) return idx;
        }
        if (raw.Length >= 1 && raw.All(char.IsDigit) && int.TryParse(raw, out var i))
        {
            if (i >= 1 && i <= options.Count) return i - 1;
            if (i >= 0 && i < options.Count) return i;
        }
        var text = ResolveCorrectAnswerText(correctAnswer, options);
        return IndexOfOption(options, text);
    }

    public static bool IsCorrect(string resolvedCorrect, string submittedAnswer, IReadOnlyList<string>? options = null)
    {
        var a = Normalize(resolvedCorrect);
        var b = Normalize(submittedAnswer);
        if (a.Length == 0 && b.Length == 0) return true;
        if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase)) return true;
        if (options is { Count: > 0 })
        {
            var correctIdx = IndexOfOption(options, a);
            var submittedIdx = IndexOfOption(options, b);
            if (correctIdx >= 0 && correctIdx == submittedIdx) return true;
        }
        return false;
    }

    public static bool IsCorrectByIndex(int correctIndex, int submittedIndex)
    {
        return correctIndex >= 0 && correctIndex == submittedIndex;
    }

    private static int IndexOfOption(IReadOnlyList<string> options, string normalizedText)
    {
        for (var i = 0; i < options.Count; i++)
            if (string.Equals(Normalize(options[i]), normalizedText, StringComparison.OrdinalIgnoreCase))
                return i;
        return -1;
    }
}

public sealed class SubmitQuizCommandHandler : IRequestHandler<SubmitQuizCommand, Result<SubmitQuizResult>>
{
    private const string WeakTopicsCacheKeyPrefix = "weak-topics:";

    private readonly IQuizRepository _quizRepository;
    private readonly IUserAnswerRepository _userAnswerRepository;
    private readonly IUserConceptProgressRepository _progressRepository;
    private readonly IQuestionConceptLinkRepository _questionConceptLinkRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cache;
    private readonly IMasteryEngine _masteryEngine;

    public SubmitQuizCommandHandler(
        IQuizRepository quizRepository,
        IUserAnswerRepository userAnswerRepository,
        IUserConceptProgressRepository progressRepository,
        IQuestionConceptLinkRepository questionConceptLinkRepository,
        IUnitOfWork unitOfWork,
        ICacheService cache,
        IMasteryEngine masteryEngine)
    {
        _quizRepository = quizRepository;
        _userAnswerRepository = userAnswerRepository;
        _progressRepository = progressRepository;
        _questionConceptLinkRepository = questionConceptLinkRepository;
        _unitOfWork = unitOfWork;
        _cache = cache;
        _masteryEngine = masteryEngine;
    }

    public async Task<Result<SubmitQuizResult>> Handle(SubmitQuizCommand request, CancellationToken cancellationToken)
    {
        if (request.Answers is null || request.Answers.Count == 0)
            return Result<SubmitQuizResult>.Failure(new AppError(ErrorCodes.ValidationFailed, "At least one answer is required.", "Answers", ErrorSeverity.Validation));

        var quiz = await _quizRepository.GetByIdAsync(request.QuizId, cancellationToken);
        if (quiz is null)
            return Result<SubmitQuizResult>.Failure(new AppError(ErrorCodes.QuizNotFound, "Quiz not found.", null, ErrorSeverity.Business));

        var questions = await _quizRepository.GetQuestionsByQuizIdAsync(request.QuizId, cancellationToken);
        var answersByQuestion = request.Answers
            .GroupBy(a => a.QuestionId)
            .ToDictionary(g => g.Key, g => g.First());

        var correctCount = 0;
        var questionResults = new List<QuestionResultItem>();
        var conceptResults = new List<ConceptAnswerResult>();

        foreach (var question in questions)
        {
            if (!answersByQuestion.TryGetValue(question.Id, out var answer))
                continue;

            var optionsList = (question.Options ?? Array.Empty<string>()).ToList();
            var submittedText = answer.SubmittedAnswer ?? "";
            var submittedIndex = answer.SubmittedOptionIndex;

            var correctText = QuizGrading.ResolveCorrectAnswerText(question.CorrectAnswer, optionsList);
            var correctIndex = QuizGrading.ResolveCorrectAnswerIndex(question.CorrectAnswer, optionsList);
            bool isCorrect;
            if (submittedIndex.HasValue && submittedIndex.Value >= 0 && submittedIndex.Value < optionsList.Count)
            {
                isCorrect = QuizGrading.IsCorrectByIndex(correctIndex, submittedIndex.Value);
            }
            else
            {
                isCorrect = QuizGrading.IsCorrect(correctText, submittedText, optionsList);
            }

            if (isCorrect) correctCount++;
            questionResults.Add(new QuestionResultItem(question.Id, isCorrect, correctText, correctIndex));

            var storedAnswerText = submittedIndex.HasValue && submittedIndex.Value >= 0 && submittedIndex.Value < optionsList.Count
                ? optionsList[submittedIndex.Value]
                : submittedText;
            var userAnswer = new UserAnswer(request.UserId, question.Id, storedAnswerText, isCorrect);
            await _userAnswerRepository.AddAsync(userAnswer, cancellationToken);

            var conceptId = await _questionConceptLinkRepository.GetConceptIdForQuestionAsync(question.Id, cancellationToken);
            if (conceptId is not null)
                conceptResults.Add(new ConceptAnswerResult(conceptId.Value, isCorrect));

            if (conceptId is null) continue;

            var progress = await _progressRepository.GetByUserAndConceptAsync(request.UserId, conceptId.Value, cancellationToken);
            var isNew = progress is null;
            if (progress is null)
            {
                progress = new UserConceptProgress(request.UserId, conceptId.Value);
                await _progressRepository.AddAsync(progress, cancellationToken);
            }

            if (isCorrect)
                progress.RecordCorrectAnswer();
            else
                progress.RecordWrongAnswer();

            if (!isNew)
                await _progressRepository.UpdateAsync(progress, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (conceptResults.Count > 0)
        {
            var masteryResult = new QuizResultForMastery(request.UserId, conceptResults);
            await _masteryEngine.UpdateFromQuizResultAsync(masteryResult, cancellationToken);
        }

        await _cache.RemoveAsync(WeakTopicsCacheKeyPrefix + request.UserId, cancellationToken);
        return Result<SubmitQuizResult>.Success(new SubmitQuizResult(correctCount, questions.Count, questionResults));
    }
}
