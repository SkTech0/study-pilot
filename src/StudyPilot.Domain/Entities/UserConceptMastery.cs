using StudyPilot.Domain.Common;

namespace StudyPilot.Domain.Entities;

/// <summary>
/// Learning intelligence: mastery and confidence per user-concept.
/// Updated by IMasteryEngine (quiz results, chat, time decay).
/// </summary>
public sealed class UserConceptMastery : BaseEntity
{
    public Guid UserId { get; private set; }
    public Guid ConceptId { get; private set; }
    /// <summary>0-100.</summary>
    public int MasteryScore { get; private set; }
    /// <summary>0-1 confidence in the score.</summary>
    public double ConfidenceScore { get; private set; }
    public int Attempts { get; private set; }
    public int CorrectAnswers { get; private set; }
    public DateTime LastInteractionUtc { get; private set; }

    public UserConceptMastery(Guid userId, Guid conceptId, int masteryScore = 0, double confidenceScore = 0)
        : base()
    {
        UserId = userId;
        ConceptId = conceptId;
        MasteryScore = Math.Clamp(masteryScore, 0, 100);
        ConfidenceScore = Math.Clamp(confidenceScore, 0, 1);
        Attempts = 0;
        CorrectAnswers = 0;
        LastInteractionUtc = DateTime.UtcNow;
    }

    private UserConceptMastery() : base() { }

    internal UserConceptMastery(Guid id, Guid userId, Guid conceptId, int masteryScore, double confidenceScore,
        int attempts, int correctAnswers, DateTime lastInteractionUtc, DateTime createdAtUtc, DateTime updatedAtUtc)
        : base(id, createdAtUtc, updatedAtUtc)
    {
        UserId = userId;
        ConceptId = conceptId;
        MasteryScore = Math.Clamp(masteryScore, 0, 100);
        ConfidenceScore = Math.Clamp(confidenceScore, 0, 1);
        Attempts = attempts;
        CorrectAnswers = correctAnswers;
        LastInteractionUtc = lastInteractionUtc;
    }

    public void ApplyCorrectAnswer(int delta = 8)
    {
        MasteryScore = Math.Clamp(MasteryScore + delta, 0, 100);
        Attempts++;
        CorrectAnswers++;
        ConfidenceScore = Math.Min(1, ConfidenceScore + 0.05);
        LastInteractionUtc = DateTime.UtcNow;
        Touch();
    }

    public void ApplyWrongAnswer(int decay = 5)
    {
        MasteryScore = Math.Clamp(MasteryScore - decay, 0, 100);
        Attempts++;
        ConfidenceScore = Math.Max(0, ConfidenceScore - 0.02);
        LastInteractionUtc = DateTime.UtcNow;
        Touch();
    }

    public void ApplyTimeDecay(double decayPerDay = 0.5)
    {
        var days = (DateTime.UtcNow - LastInteractionUtc).TotalDays;
        if (days <= 0) return;
        var drop = (int)(days * decayPerDay);
        if (drop <= 0) return;
        MasteryScore = Math.Clamp(MasteryScore - drop, 0, 100);
        Touch();
    }
}
