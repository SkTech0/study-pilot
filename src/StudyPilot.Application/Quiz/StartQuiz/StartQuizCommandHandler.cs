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
    private readonly IUserConceptMasteryRepository _masteryRepository;
    private readonly IQuizConceptOrderRepository _conceptOrderRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUsageGuardService _usageGuard;

    public StartQuizCommandHandler(
        IDocumentRepository documentRepository,
        IConceptRepository conceptRepository,
        IQuizRepository quizRepository,
        IUserConceptMasteryRepository masteryRepository,
        IQuizConceptOrderRepository conceptOrderRepository,
        IUnitOfWork unitOfWork,
        IUsageGuardService usageGuard)
    {
        _documentRepository = documentRepository;
        _conceptRepository = conceptRepository;
        _quizRepository = quizRepository;
        _masteryRepository = masteryRepository;
        _conceptOrderRepository = conceptOrderRepository;
        _unitOfWork = unitOfWork;
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
        if (concepts.Count == 0)
            return Result<StartQuizResult>.Failure(new AppError(ErrorCodes.DocumentNoConcepts, "Document has no concepts. Process the document first.", null, ErrorSeverity.Business));

        var totalQuestionCount = Math.Min(5, Math.Max(1, concepts.Count));
        var conceptIds = concepts.Select(c => c.Id).ToList();
        var masteries = await _masteryRepository.GetByUserAndConceptsAsync(request.UserId, conceptIds, cancellationToken);
        var masteryByConcept = masteries.ToDictionary(m => m.ConceptId, m => m.MasteryScore);
        var withMastery = concepts.Select(c => new ConceptWithMastery(c.Id, masteryByConcept.TryGetValue(c.Id, out var s) ? s : 0)).ToList();
        var orderedConceptIds = AdaptiveQuizComposition.BuildOrderedConceptIds(withMastery, totalQuestionCount);

        var quiz = new Domain.Entities.Quiz(request.DocumentId, request.UserId, totalQuestionCount);

        await _quizRepository.AddAsync(quiz, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (orderedConceptIds.Count > 0)
        {
            await _conceptOrderRepository.SetConceptOrderAsync(quiz.Id, orderedConceptIds, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result<StartQuizResult>.Success(new StartQuizResult(quiz.Id, totalQuestionCount, Array.Empty<StartQuizQuestionSummary>()));
    }
}
