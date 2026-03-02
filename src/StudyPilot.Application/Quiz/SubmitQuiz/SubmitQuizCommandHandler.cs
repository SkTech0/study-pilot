using System.Collections.Generic;
using StudyPilot.Application.Abstractions.Persistence;
using StudyPilot.Application.Common.Errors;
using StudyPilot.Application.Common.Models;
using MediatR;
using StudyPilot.Domain.Entities;

namespace StudyPilot.Application.Quiz.SubmitQuiz;

internal static class QuizGrading
{
    /// <summary>Resolve stored correct answer (option text, or letter A–D, or 1–4) to the exact option text for comparison.</summary>
    public static string ResolveCorrectAnswerText(string correctAnswer, IReadOnlyList<string> options)
    {
        if (options.Count == 0) return correctAnswer?.Trim() ?? "";
        var raw = (correctAnswer ?? "").Trim();
        if (raw.Length == 1 && raw[0] is >= 'A' and <= 'D')
        {
            var idx = raw[0] - 'A';
            if (idx < options.Count) return options[idx].Trim();
        }
        if (raw.Length == 1 && char.IsDigit(raw[0]))
        {
            var i = raw[0] - '0';
            if (i >= 1 && i <= options.Count) return options[i - 1].Trim();
            if (i < options.Count) return options[i].Trim();
        }
        return raw;
    }

    public static bool IsCorrect(string resolvedCorrect, string submittedAnswer)
    {
        var a = (resolvedCorrect ?? "").Trim();
        var b = (submittedAnswer ?? "").Trim();
        if (a.Length == 0 && b.Length == 0) return true;
        return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class SubmitQuizCommandHandler : IRequestHandler<SubmitQuizCommand, Result<SubmitQuizResult>>
{
    private readonly IQuizRepository _quizRepository;
    private readonly IUserAnswerRepository _userAnswerRepository;
    private readonly IUserConceptProgressRepository _progressRepository;
    private readonly IQuestionConceptLinkRepository _questionConceptLinkRepository;
    private readonly IUnitOfWork _unitOfWork;

    public SubmitQuizCommandHandler(
        IQuizRepository quizRepository,
        IUserAnswerRepository userAnswerRepository,
        IUserConceptProgressRepository progressRepository,
        IQuestionConceptLinkRepository questionConceptLinkRepository,
        IUnitOfWork unitOfWork)
    {
        _quizRepository = quizRepository;
        _userAnswerRepository = userAnswerRepository;
        _progressRepository = progressRepository;
        _questionConceptLinkRepository = questionConceptLinkRepository;
        _unitOfWork = unitOfWork;
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

            var correctText = QuizGrading.ResolveCorrectAnswerText(question.CorrectAnswer, question.Options.ToList());
            var isCorrect = QuizGrading.IsCorrect(correctText, submittedAnswer);
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
        return Result<SubmitQuizResult>.Success(new SubmitQuizResult(correctCount, quiz.Questions.Count));
    }
}
