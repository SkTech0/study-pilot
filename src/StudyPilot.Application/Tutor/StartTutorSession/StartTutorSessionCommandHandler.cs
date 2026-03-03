using MediatR;
using StudyPilot.Application.Abstractions.Persistence;
using StudyPilot.Application.Common.Models;
using StudyPilot.Domain.Entities;
using StudyPilot.Domain.Enums;

namespace StudyPilot.Application.Tutor.StartTutorSession;

public sealed class StartTutorSessionCommandHandler : IRequestHandler<StartTutorSessionCommand, Result<StartTutorSessionResult>>
{
    private const int MinGoals = 3;
    private const int MaxGoals = 5;
    private const int WeakThreshold = 40;

    private readonly ITutorSessionRepository _sessionRepository;
    private readonly ILearningGoalRepository _goalRepository;
    private readonly IUserConceptMasteryRepository _masteryRepository;
    private readonly IConceptRepository _conceptRepository;
    private readonly IUnitOfWork _unitOfWork;

    public StartTutorSessionCommandHandler(
        ITutorSessionRepository sessionRepository,
        ILearningGoalRepository goalRepository,
        IUserConceptMasteryRepository masteryRepository,
        IConceptRepository conceptRepository,
        IUnitOfWork unitOfWork)
    {
        _sessionRepository = sessionRepository;
        _goalRepository = goalRepository;
        _masteryRepository = masteryRepository;
        _conceptRepository = conceptRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<StartTutorSessionResult>> Handle(StartTutorSessionCommand request, CancellationToken cancellationToken)
    {
        var session = new TutorSession(request.UserId, request.DocumentId);
        await _sessionRepository.AddAsync(session, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var masteryList = await _masteryRepository.GetByUserIdAsync(request.UserId, cancellationToken);
        var weak = masteryList
            .Where(m => m.MasteryScore <= WeakThreshold)
            .OrderBy(m => m.MasteryScore)
            .Take(MaxGoals)
            .ToList();

        if (request.DocumentId.HasValue)
        {
            var docConcepts = await _conceptRepository.GetByDocumentIdAsync(request.DocumentId.Value, cancellationToken);
            var docConceptIds = docConcepts.Select(c => c.Id).ToHashSet();
            weak = weak.Where(m => docConceptIds.Contains(m.ConceptId)).Take(MaxGoals).ToList();
        }

        var conceptIds = weak.Select(m => m.ConceptId).Distinct().ToList();
        var concepts = conceptIds.Count > 0 ? await _conceptRepository.GetByIdsAsync(conceptIds, cancellationToken) : new List<Concept>();
        var conceptMap = concepts.ToDictionary(c => c.Id);

        var goals = new List<LearningGoal>();
        var priority = 0;
        foreach (var m in weak.Take(Math.Max(MinGoals, Math.Min(MaxGoals, weak.Count))))
        {
            if (!conceptMap.TryGetValue(m.ConceptId, out var concept)) continue;
            var goalType = m.MasteryScore < 20 ? LearningGoalType.Understand : m.MasteryScore < 50 ? LearningGoalType.Revise : LearningGoalType.Master;
            goals.Add(new LearningGoal(request.UserId, session.Id, concept.Id, goalType, priority++));
        }

        if (goals.Count == 0 && conceptIds.Count > 0)
        {
            foreach (var c in concepts.Take(MaxGoals))
                goals.Add(new LearningGoal(request.UserId, session.Id, c.Id, LearningGoalType.Understand, priority++));
        }

        await _goalRepository.AddRangeAsync(goals, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var summaries = goals
            .Where(g => conceptMap.TryGetValue(g.ConceptId, out _))
            .Select(g => new LearningGoalSummary(g.Id, g.ConceptId, conceptMap[g.ConceptId].Name, g.GoalType.ToString(), g.Priority))
            .ToList();

        return Result<StartTutorSessionResult>.Success(new StartTutorSessionResult(session.Id, summaries));
    }
}
