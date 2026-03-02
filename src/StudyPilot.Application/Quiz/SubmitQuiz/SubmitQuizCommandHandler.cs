using StudyPilot.Application.Abstractions.Persistence;
using StudyPilot.Application.Common.Errors;
using StudyPilot.Application.Common.Models;
using MediatR;
using StudyPilot.Domain.Entities;

namespace StudyPilot.Application.Quiz.SubmitQuiz;

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

            var isCorrect = string.Equals(question.CorrectAnswer.Trim(), submittedAnswer.Trim(), StringComparison.OrdinalIgnoreCase);
            if (isCorrect) correctCount++;

            var userAnswer = new UserAnswer(request.UserId, question.Id, submittedAnswer, isCorrect);
            await _userAnswerRepository.AddAsync(userAnswer, cancellationToken);

            var conceptId = await _questionConceptLinkRepository.GetConceptIdForQuestionAsync(question.Id, cancellationToken);
            if (conceptId is null) continue;

            var progress = await _progressRepository.GetByUserAndConceptAsync(request.UserId, conceptId.Value, cancellationToken);
            if (progress is null)
            {
                progress = new UserConceptProgress(request.UserId, conceptId.Value);
                await _progressRepository.AddAsync(progress, cancellationToken);
            }

            if (isCorrect)
                progress.RecordCorrectAnswer();
            else
                progress.RecordWrongAnswer();
            await _progressRepository.UpdateAsync(progress, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<SubmitQuizResult>.Success(new SubmitQuizResult(correctCount, quiz.Questions.Count));
    }
}
