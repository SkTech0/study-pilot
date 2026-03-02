using StudyPilot.Domain.Common;
using StudyPilot.Domain.ValueObjects;

namespace StudyPilot.Domain.Entities;

public class UserConceptProgress : BaseEntity
{
    public Guid UserId { get; private set; }
    public Guid ConceptId { get; private set; }
    public MasteryScore MasteryScore { get; private set; }
    public int Attempts { get; private set; }
    public double Accuracy => Attempts > 0 ? (double)_correctCount / Attempts : 0;
    public DateTime LastReviewedUtc { get; private set; }

    private int _correctCount;

    public UserConceptProgress(Guid userId, Guid conceptId) : base()
    {
        UserId = userId;
        ConceptId = conceptId;
        MasteryScore = MasteryScore.Create(0);
        Attempts = 0;
        _correctCount = 0;
        LastReviewedUtc = DateTime.UtcNow;
    }

    public UserConceptProgress(Guid userId, Guid conceptId, int initialMastery, int attempts, int correctCount, DateTime lastReviewedUtc, Guid id, DateTime createdAtUtc, DateTime updatedAtUtc)
        : base(id, createdAtUtc, updatedAtUtc)
    {
        UserId = userId;
        ConceptId = conceptId;
        MasteryScore = MasteryScore.Create(initialMastery);
        Attempts = attempts;
        _correctCount = correctCount;
        LastReviewedUtc = lastReviewedUtc;
    }

    public void RecordCorrectAnswer()
    {
        MasteryScore = MasteryScore.Increase(10);
        Attempts++;
        _correctCount++;
        LastReviewedUtc = DateTime.UtcNow;
        Touch();
    }

    public void RecordWrongAnswer()
    {
        MasteryScore = MasteryScore.Decrease(5);
        Attempts++;
        LastReviewedUtc = DateTime.UtcNow;
        Touch();
    }
}
