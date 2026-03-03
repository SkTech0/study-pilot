using StudyPilot.Domain.Common;
using StudyPilot.Domain.Enums;

namespace StudyPilot.Domain.Entities;

public sealed class LearningGoal : BaseEntity
{
    public Guid UserId { get; private set; }
    public Guid TutorSessionId { get; private set; }
    public Guid ConceptId { get; private set; }
    public LearningGoalType GoalType { get; private set; }
    public int Priority { get; private set; }
    public int ProgressPercent { get; private set; }
    public DateTime CreatedUtc { get; private set; }

    public LearningGoal(Guid userId, Guid tutorSessionId, Guid conceptId, LearningGoalType goalType, int priority) : base()
    {
        UserId = userId;
        TutorSessionId = tutorSessionId;
        ConceptId = conceptId;
        GoalType = goalType;
        Priority = Math.Max(0, priority);
        ProgressPercent = 0;
        CreatedUtc = DateTime.UtcNow;
    }

    internal LearningGoal(Guid id, Guid userId, Guid tutorSessionId, Guid conceptId, LearningGoalType goalType,
        int priority, int progressPercent, DateTime createdUtc, DateTime createdAtUtc, DateTime updatedAtUtc)
        : base(id, createdAtUtc, updatedAtUtc)
    {
        UserId = userId;
        TutorSessionId = tutorSessionId;
        ConceptId = conceptId;
        GoalType = goalType;
        Priority = priority;
        ProgressPercent = Math.Clamp(progressPercent, 0, 100);
        CreatedUtc = createdUtc;
    }

    public void SetProgress(int percent)
    {
        ProgressPercent = Math.Clamp(percent, 0, 100);
        Touch();
    }
}
