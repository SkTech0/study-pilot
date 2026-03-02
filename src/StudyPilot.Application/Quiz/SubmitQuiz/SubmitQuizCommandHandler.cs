using System.Collections.Generic;
using StudyPilot.Application.Abstractions.Caching;
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
        return string.Join(" ", (s.Trim() ?? "").Split([' ', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries)).Trim();
    }

    /// <summary>Resolve stored correct answer (option text, or letter A–D, or 1–4) to the exact option text for comparison.</summary>
    public static string ResolveCorrectAnswerText(string correctAnswer, IReadOnlyList<string> options)
    {
        if (options.Count == 0) return Normalize(correctAnswer);
        var raw = Normalize(correctAnswer);
        if (raw.Length == 1 && raw[0] is >= 'A' and <= 'D')
        {
            var idx = raw[0] - 'A';
            if (idx < options.Count) return Normalize(options[idx]);
        }
        if (raw.Length == 1 && char.IsDigit(raw[0]))
        {
            var i = raw[0] - '0';
            if (i >= 1 && i <= options.Count) return Normalize(options[i - 1]);
            if (i < options.Count) return Normalize(options[i]);
        }
        return raw;
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

    public SubmitQuizCommandHandler(
        IQuizRepository quizRepository,
        IUserAnswerRepository userAnswerRepository,
        IUserConceptProgressRepository progressRepository,
        IQuestionConceptLinkRepository questionConceptLinkRepository,
        IUnitOfWork unitOfWork,
        ICacheService cache)
    {
        _quizRepository = quizRepository;
        _userAnswerRepository = userAnswerRepository;
        _progressRepository = progressRepository;
        _questionConceptLinkRepository = questionConceptLinkRepository;
        _unitOfWork = unitOfWork;
        _cache = cache;
    }

    public async Task<Result<SubmitQuizResult>> Handle(SubmitQuizCommand request, CancellationToken cancellationToken)
    {
        var quiz = await _quizRepository.GetByIdWithQuestionsAsync(request.QuizId, cancellationToken);
        if (quiz is null)
            return Result<SubmitQuizResult>.Failure(new AppError(ErrorCodes.QuizNotFound, "Quiz not found.", null, ErrorSeverity.Business));

        var answerMap = request.Answers.ToDictionary(a => a.QuestionId, a => a.SubmittedAnswer);
        var correctCount = 0;

        foreach (var question in quiz.Questions)
        {
            if (!answerMap.TryGetValue(question.Id, out var submittedAnswer))
                continue;

            var optionsList = question.Options.ToList();
            var correctText = QuizGrading.ResolveCorrectAnswerText(question.CorrectAnswer, optionsList);
            var isCorrect = QuizGrading.IsCorrect(correctText, submittedAnswer, optionsList);
            if (isCorrect) correctCount++;

            var userAnswer = new UserAnswer(request.UserId, question.Id, submittedAnswer, isCorrect);
            await _userAnswerRepository.AddAsync(userAnswer, cancellationToken);

            var conceptId = await _questionConceptLinkRepository.GetConceptIdForQuestionAsync(question.Id, cancellationToken);
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
        await _cache.RemoveAsync(WeakTopicsCacheKeyPrefix + request.UserId, cancellationToken);
        return Result<SubmitQuizResult>.Success(new SubmitQuizResult(correctCount, quiz.Questions.Count));
    }
}
