using StudyPilot.Domain.Common;
using StudyPilot.Domain.Enums;

namespace StudyPilot.Domain.Entities;

/// <summary>
/// Long-term learning memory: confusion, improvement, repeated mistakes.
/// Created by background intelligence job from interaction analysis.
/// </summary>
public sealed class LearningInsight : BaseEntity
{
    public Guid UserId { get; private set; }
    public Guid ConceptId { get; private set; }
    public LearningInsightType InsightType { get; private set; }
    public InsightSourceType Source { get; private set; }
    public double Confidence { get; private set; }
    public DateTime CreatedUtc { get; private set; }

    public LearningInsight(Guid userId, Guid conceptId, LearningInsightType insightType, InsightSourceType source, double confidence)
        : base()
    {
        UserId = userId;
        ConceptId = conceptId;
        InsightType = insightType;
        Source = source;
        Confidence = Math.Clamp(confidence, 0, 1);
        CreatedUtc = DateTime.UtcNow;
    }

    private LearningInsight() : base() { }

    internal LearningInsight(Guid id, Guid userId, Guid conceptId, LearningInsightType insightType, InsightSourceType source,
        double confidence, DateTime createdUtc, DateTime createdAtUtc, DateTime updatedAtUtc)
        : base(id, createdAtUtc, updatedAtUtc)
    {
        UserId = userId;
        ConceptId = conceptId;
        InsightType = insightType;
        Source = source;
        Confidence = confidence;
        CreatedUtc = createdUtc;
    }
}
