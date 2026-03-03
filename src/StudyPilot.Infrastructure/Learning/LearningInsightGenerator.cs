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
        var insights = new List<LearningInsight>();

        foreach (var userId in userIds)
        {
            var list = await _masteryRepository.GetByUserIdAsync(userId, cancellationToken);
            foreach (var m in list)
            {
                if (m.Attempts >= MinAttemptsForMistake)
                {
                    var accuracy = (double)m.CorrectAnswers / m.Attempts;
                    if (accuracy < LowAccuracyThreshold)
                    {
                        var exists = await _insightRepository.ExistsAsync(userId, m.ConceptId, LearningInsightType.RepeatedMistake, since, cancellationToken);
                        if (!exists)
                        {
                            insights.Add(new LearningInsight(userId, m.ConceptId, LearningInsightType.RepeatedMistake, InsightSourceType.Quiz, 0.8));
                        }
                    }
                    else if (accuracy >= HighAccuracyThreshold && m.Attempts >= MinAttemptsForImprovement)
                    {
                        var exists = await _insightRepository.ExistsAsync(userId, m.ConceptId, LearningInsightType.Improvement, since, cancellationToken);
                        if (!exists)
                        {
                            insights.Add(new LearningInsight(userId, m.ConceptId, LearningInsightType.Improvement, InsightSourceType.Quiz, 0.7));
                        }
                    }
                }
            }
        }

        if (insights.Count > 0)
        {
            await _insightRepository.AddRangeAsync(insights, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            StudyPilotMetrics.LearningInsightsGenerated.Add(insights.Count);
        }
    }
}
