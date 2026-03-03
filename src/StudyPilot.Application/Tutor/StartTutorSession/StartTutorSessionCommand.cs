using MediatR;
using StudyPilot.Application.Common.Models;

namespace StudyPilot.Application.Tutor.StartTutorSession;

public sealed record StartTutorSessionCommand(Guid UserId, Guid? DocumentId) : IRequest<Result<StartTutorSessionResult>>;

public sealed record StartTutorSessionResult(
    Guid SessionId,
    IReadOnlyList<LearningGoalSummary> Goals);

public sealed record LearningGoalSummary(Guid GoalId, Guid ConceptId, string ConceptName, string GoalType, int Priority);
