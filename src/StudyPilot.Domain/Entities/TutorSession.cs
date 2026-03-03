using StudyPilot.Domain.Common;
using StudyPilot.Domain.Enums;

namespace StudyPilot.Domain.Entities;

public sealed class TutorSession : BaseEntity
{
    public Guid UserId { get; private set; }
    public Guid? DocumentId { get; private set; }
    public TutorSessionState SessionState { get; private set; }
    public TutorStep CurrentStep { get; private set; }
    public Guid? CurrentGoalId { get; private set; }
    public DateTime StartedUtc { get; private set; }
    public DateTime LastInteractionUtc { get; private set; }

    public TutorSession(Guid userId, Guid? documentId) : base()
    {
        UserId = userId;
        DocumentId = documentId;
        SessionState = TutorSessionState.Active;
        CurrentStep = TutorStep.Diagnose;
        CurrentGoalId = null;
        var now = DateTime.UtcNow;
        StartedUtc = now;
        LastInteractionUtc = now;
    }

    internal TutorSession(Guid id, Guid userId, Guid? documentId, TutorSessionState sessionState,
        TutorStep currentStep, Guid? currentGoalId, DateTime startedUtc, DateTime lastInteractionUtc,
        DateTime createdAtUtc, DateTime updatedAtUtc) : base(id, createdAtUtc, updatedAtUtc)
    {
        UserId = userId;
        DocumentId = documentId;
        SessionState = sessionState;
        CurrentStep = currentStep;
        CurrentGoalId = currentGoalId;
        StartedUtc = startedUtc;
        LastInteractionUtc = lastInteractionUtc;
    }

    public void SetStep(TutorStep step)
    {
        CurrentStep = step;
        LastInteractionUtc = DateTime.UtcNow;
        Touch();
    }

    public void SetCurrentGoal(Guid? goalId)
    {
        CurrentGoalId = goalId;
        Touch();
    }

    public void Complete()
    {
        SessionState = TutorSessionState.Completed;
        Touch();
    }

    public void Abandon()
    {
        SessionState = TutorSessionState.Abandoned;
        Touch();
    }

    public void TouchInteraction()
    {
        LastInteractionUtc = DateTime.UtcNow;
        Touch();
    }
}
