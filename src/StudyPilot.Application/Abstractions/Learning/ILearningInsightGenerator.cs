namespace StudyPilot.Application.Abstractions.Learning;

/// <summary>
/// Analyzes user interactions to create LearningInsight records (RepeatedMistake, Improvement, Confusion).
/// Called by LearningIntelligenceWorker.
/// </summary>
public interface ILearningInsightGenerator
{
    Task GenerateInsightsAsync(CancellationToken cancellationToken = default);
}
