using MediatR;
using StudyPilot.Application.Abstractions.Knowledge;
using StudyPilot.Application.Abstractions.Persistence;
using StudyPilot.Application.Abstractions.Tutor;
using StudyPilot.Application.Common.Errors;
using StudyPilot.Application.Common.Models;
using StudyPilot.Application.Knowledge;
using StudyPilot.Application.Knowledge.Models;
using StudyPilot.Application.Tutor.Constants;
using StudyPilot.Application.Tutor.Models;
using StudyPilot.Domain.Entities;
using StudyPilot.Domain.Enums;

namespace StudyPilot.Application.Tutor.TutorRespond;

public sealed class TutorRespondCommandHandler : IRequestHandler<TutorRespondCommand, Result<TutorRespondResult>>
{
    private readonly ITutorSessionRepository _sessionRepository;
    private readonly ILearningGoalRepository _goalRepository;
    private readonly ITutorMessageRepository _messageRepository;
    private readonly ITutorExerciseRepository _exerciseRepository;
    private readonly IConceptRepository _conceptRepository;
    private readonly IUserConceptMasteryRepository _masteryRepository;
    private readonly ILearningInsightRepository _insightRepository;
    private readonly IEmbeddingService _embeddingService;
    private readonly IQueryEmbeddingCache _embeddingCache;
    private readonly IHybridSearchService _hybridSearch;
    private readonly ITutorService _tutorService;
    private readonly IUnitOfWork _unitOfWork;

    public TutorRespondCommandHandler(
        ITutorSessionRepository sessionRepository,
        ILearningGoalRepository goalRepository,
        ITutorMessageRepository messageRepository,
        ITutorExerciseRepository exerciseRepository,
        IConceptRepository conceptRepository,
        IUserConceptMasteryRepository masteryRepository,
        ILearningInsightRepository insightRepository,
        IEmbeddingService embeddingService,
        IQueryEmbeddingCache embeddingCache,
        IHybridSearchService hybridSearch,
        ITutorService tutorService,
        IUnitOfWork unitOfWork)
    {
        _sessionRepository = sessionRepository;
        _goalRepository = goalRepository;
        _messageRepository = messageRepository;
        _exerciseRepository = exerciseRepository;
        _conceptRepository = conceptRepository;
        _masteryRepository = masteryRepository;
        _insightRepository = insightRepository;
        _embeddingService = embeddingService;
        _embeddingCache = embeddingCache;
        _hybridSearch = hybridSearch;
        _tutorService = tutorService;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<TutorRespondResult>> Handle(TutorRespondCommand request, CancellationToken cancellationToken)
    {
        var session = await _sessionRepository.GetByIdAndUserIdAsync(request.SessionId, request.UserId, cancellationToken);
        if (session is null)
            return Result<TutorRespondResult>.Failure(new AppError(ErrorCodes.NotFound, "Tutor session not found.", null, ErrorSeverity.Business));
        if (session.SessionState != TutorSessionState.Active)
            return Result<TutorRespondResult>.Failure(new AppError(ErrorCodes.BusinessRuleViolation, "Session is not active.", null, ErrorSeverity.Business));

        var message = (request.Message ?? "").Trim();
        if (message.Length == 0)
            return Result<TutorRespondResult>.Failure(new AppError(ErrorCodes.ValidationError, "Message is required.", null, ErrorSeverity.Business));
        if (message.Length > TutorConstants.MaxUserMessageLength)
            message = message[..TutorConstants.MaxUserMessageLength];

        await _messageRepository.AddAsync(new TutorMessage(session.Id, "user", message), cancellationToken);
        session.TouchInteraction();
        await _sessionRepository.UpdateAsync(session, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var goals = await _goalRepository.GetByTutorSessionIdAsync(session.Id, cancellationToken);
        var conceptIds = goals.Select(g => g.ConceptId).Distinct().ToList();
        var concepts = conceptIds.Count > 0 ? await _conceptRepository.GetByIdsAsync(conceptIds, cancellationToken) : new List<Concept>();
        var conceptMap = concepts.ToDictionary(c => c.Id);
        var masteryList = conceptIds.Count > 0 ? await _masteryRepository.GetByUserAndConceptsAsync(request.UserId, conceptIds, cancellationToken) : new List<UserConceptMastery>();
        var masteryByConcept = masteryList.ToDictionary(m => m.ConceptId);

        var recentMistakes = new List<string>();
        var insights = await _insightRepository.GetByUserIdAsync(request.UserId, TutorConstants.RecentMistakesLimit, cancellationToken);
        foreach (var i in insights.Where(x => x.InsightType == Domain.Enums.LearningInsightType.RepeatedMistake))
        {
            if (conceptMap.TryGetValue(i.ConceptId, out var c))
                recentMistakes.Add(c.Name);
        }

        var queryEmbedding = await _embeddingCache.GetAsync(message, cancellationToken);
        if (queryEmbedding is null)
        {
            queryEmbedding = await _embeddingService.EmbedAsync(message, cancellationToken);
            await _embeddingCache.SetAsync(message, queryEmbedding, cancellationToken);
        }
        var retrieved = await _hybridSearch.SearchAsync(request.UserId, queryEmbedding, session.DocumentId, message, TutorConstants.TutorRetrievalTopK, cancellationToken);
        var chunks = retrieved.Select(c => new TutorContextChunk(c.ChunkId, c.DocumentId, c.Text)).ToList();

        var goalInfos = goals
            .Where(g => conceptMap.TryGetValue(g.ConceptId, out _))
            .Select(g => new TutorGoalInfo(g.Id, g.ConceptId, conceptMap[g.ConceptId].Name, g.GoalType.ToString(), g.ProgressPercent))
            .ToList();
        var masteryInfos = conceptIds
            .Where(id => conceptMap.TryGetValue(id, out _))
            .Select(id => new TutorMasteryInfo(id, conceptMap[id].Name, masteryByConcept.TryGetValue(id, out var m) ? m.MasteryScore : 0))
            .ToList();

        var avgMastery = masteryInfos.Count > 0 ? masteryInfos.Average(x => x.MasteryScore) : 50;
        var tone = avgMastery < 40 ? TutorTone.Supportive : avgMastery < 70 ? TutorTone.Neutral : TutorTone.Challenging;
        var explanationStyle = ExplanationStyleResolver.FromAverageMastery(avgMastery).ToString();

        var context = new TutorContext(
            request.UserId,
            session.Id,
            message,
            session.CurrentStep,
            goalInfos,
            masteryInfos,
            recentMistakes,
            explanationStyle,
            tone,
            chunks);

        var response = await _tutorService.RespondAsync(context, cancellationToken);

        var assistantText = response.Message.Length > TutorConstants.MaxAssistantMessageLength
            ? response.Message[..TutorConstants.MaxAssistantMessageLength]
            : response.Message;

        await _messageRepository.AddAsync(new TutorMessage(session.Id, "assistant", assistantText), cancellationToken);
        session.SetStep(response.NextStep);
        if (session.CurrentGoalId.HasValue && response.NextStep == TutorStep.Complete)
        {
            var goal = await _goalRepository.GetByIdAsync(session.CurrentGoalId.Value, cancellationToken);
            if (goal != null)
            {
                goal.SetProgress(100);
                await _goalRepository.UpdateAsync(goal, cancellationToken);
            }
            session.SetCurrentGoal(null);
        }

        TutorExerciseResult? exerciseResult = null;
        if (response.OptionalExercise is not null)
        {
            var currentGoal = goals.FirstOrDefault(g => g.Id == session.CurrentGoalId);
            var conceptId = currentGoal?.ConceptId ?? (conceptIds.Count > 0 ? conceptIds[0] : Guid.Empty);
            var ex = new TutorExercise(session.Id, conceptId, response.OptionalExercise.Question, response.OptionalExercise.ExpectedAnswer, response.OptionalExercise.Difficulty);
            await _exerciseRepository.AddAsync(ex, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            exerciseResult = new TutorExerciseResult(ex.Id, response.OptionalExercise.Question, response.OptionalExercise.ExpectedAnswer, response.OptionalExercise.Difficulty);
        }

        await _sessionRepository.UpdateAsync(session, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<TutorRespondResult>.Success(new TutorRespondResult(
            assistantText,
            response.NextStep.ToString(),
            exerciseResult,
            response.CitedChunkIds ?? new List<Guid>()));
    }
}
