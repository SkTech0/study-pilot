using StudyPilot.Application.Abstractions.Learning;
using StudyPilot.Application.Abstractions.Persistence;
using StudyPilot.Domain.Entities;
using StudyPilot.Domain.Enums;
using StudyPilot.Infrastructure.Services;

namespace StudyPilot.Infrastructure.Learning;

public sealed class LearningInsightGenerator : ILearningInsightGenerator
{
    private const int MinAttemptsForMistake = 3;
    private const double LowAccuracyThreshold = 0.5;
    private const int MinAttemptsForImprovement = 2;
    private const double HighAccuracyThreshold = 0.8;
    private static readonly TimeSpan InsightCooldown = TimeSpan.FromDays(7);

    private readonly IUserConceptMasteryRepository _masteryRepository;
    private readonly ILearningInsightRepository _insightRepository;
    private readonly IUnitOfWork _unitOfWork;

    public LearningInsightGenerator(
        IUserConceptMasteryRepository masteryRepository,
        ILearningInsightRepository insightRepository,
        IUnitOfWork unitOfWork)
    {
        _masteryRepository = masteryRepository;
        _insightRepository = insightRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task GenerateInsightsAsync(CancellationToken cancellationToken = default)
    {
        var since = DateTime.UtcNow - InsightCooldown;
        var userIds = await _masteryRepository.GetDistinctUserIdsAsync(cancellationToken);
        if (userIds.Count == 0) return;
        var candidates = new List<(Guid userId, Guid conceptId, LearningInsightType type)>();
        foreach (var userId in userIds)
        {
            var list = await _masteryRepository.GetByUserIdAsync(userId, cancellationToken);
            foreach (var m in list)
            {
                if (m.Attempts >= MinAttemptsForMistake)
                {
                    var accuracy = (double)m.CorrectAnswers / m.Attempts;
                    if (accuracy < LowAccuracyThreshold)
                        candidates.Add((userId, m.ConceptId, LearningInsightType.RepeatedMistake));
                    else if (accuracy >= HighAccuracyThreshold && m.Attempts >= MinAttemptsForImprovement)
                        candidates.Add((userId, m.ConceptId, LearningInsightType.Improvement));
                }
            }
        }
        if (candidates.Count == 0) return;
        var keys = candidates.Select(c => (c.userId, c.conceptId)).Distinct().ToList();
        var existing = await _insightRepository.GetExistingKeysAsync(keys, since, cancellationToken);
        var insights = new List<LearningInsight>();
        var added = new HashSet<(Guid, Guid, LearningInsightType)>();
        foreach (var (userId, conceptId, type) in candidates)
        {
            if (existing.Contains((userId, conceptId, type)) || !added.Add((userId, conceptId, type))) continue;
            insights.Add(new LearningInsight(userId, conceptId, type, InsightSourceType.Quiz, type == LearningInsightType.RepeatedMistake ? 0.8 : 0.7));
        }

        if (insights.Count > 0)
        {
            await _insightRepository.AddRangeAsync(insights, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            StudyPilotMetrics.LearningInsightsGenerated.Add(insights.Count);
        }
    }
}
