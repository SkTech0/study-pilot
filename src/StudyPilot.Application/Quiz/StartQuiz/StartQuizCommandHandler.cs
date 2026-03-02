using StudyPilot.Application.Abstractions.AI;
using StudyPilot.Application.Abstractions.Persistence;
using StudyPilot.Application.Abstractions.UsageGuard;
using StudyPilot.Application.Common.Errors;
using StudyPilot.Application.Common.Models;
using MediatR;
using StudyPilot.Domain.Entities;

namespace StudyPilot.Application.Quiz.StartQuiz;

public sealed class StartQuizCommandHandler : IRequestHandler<StartQuizCommand, Result<StartQuizResult>>
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IConceptRepository _conceptRepository;
    private readonly IQuizRepository _quizRepository;
    private readonly IQuestionConceptLinkRepository _questionConceptLinkRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAIService _aiService;
    private readonly IUsageGuardService _usageGuard;

    public StartQuizCommandHandler(
        IDocumentRepository documentRepository,
        IConceptRepository conceptRepository,
        IQuizRepository quizRepository,
        IQuestionConceptLinkRepository questionConceptLinkRepository,
        IUnitOfWork unitOfWork,
        IAIService aiService,
        IUsageGuardService usageGuard)
    {
        _documentRepository = documentRepository;
        _conceptRepository = conceptRepository;
        _quizRepository = quizRepository;
        _questionConceptLinkRepository = questionConceptLinkRepository;
        _unitOfWork = unitOfWork;
        _aiService = aiService;
        _usageGuard = usageGuard;
    }

    public async Task<Result<StartQuizResult>> Handle(StartQuizCommand request, CancellationToken cancellationToken)
    {
        if (!await _usageGuard.CanGenerateQuizAsync(request.UserId, cancellationToken))
            return Result<StartQuizResult>.Failure(new AppError(ErrorCodes.QuizGenerationLimitReached, "Quiz generation limit reached. Try again later.", null, ErrorSeverity.Business));
        var document = await _documentRepository.GetByIdAsync(request.DocumentId, cancellationToken);
        if (document is null)
            return Result<StartQuizResult>.Failure(new AppError(ErrorCodes.DocumentNotFound, "Document not found.", null, ErrorSeverity.Business));

        var concepts = await _conceptRepository.GetByDocumentIdAsync(request.DocumentId, cancellationToken);
        var conceptInfos = concepts.Select(c => new ConceptInfo(c.Id, c.Name, c.Description)).ToList();
        if (conceptInfos.Count == 0)
            return Result<StartQuizResult>.Failure(new AppError(ErrorCodes.DocumentNoConcepts, "Document has no concepts. Process the document first.", null, ErrorSeverity.Business));

        var quizResult = await _aiService.GenerateQuizAsync(request.DocumentId, request.UserId, conceptInfos, cancellationToken);
        var quiz = new Domain.Entities.Quiz(request.DocumentId, request.UserId);

        foreach (var gq in quizResult.Questions)
        {
            var question = new Domain.Entities.Question(quiz.Id, gq.Text, gq.QuestionType, gq.CorrectAnswer, gq.Options.ToList());
            quiz.AddQuestion(question);
            await _questionConceptLinkRepository.AddAsync(question.Id, gq.ConceptId, cancellationToken);
        }

        await _quizRepository.AddAsync(quiz, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var questions = quiz.Questions
            .Select(q => new StartQuizQuestionSummary(q.Id, q.Text, q.Options.ToList()))
            .ToList();
        return Result<StartQuizResult>.Success(new StartQuizResult(quiz.Id, questions));
    }
}
