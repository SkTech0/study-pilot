using MediatR;
using StudyPilot.Application.Common.Models;

namespace StudyPilot.Application.Learning.GetLearningProgress;

public sealed record GetLearningProgressQuery(Guid UserId) : IRequest<Result<LearningProgressResult>>;

public sealed record LearningProgressResult(
    IReadOnlyList<ConceptProgressItem> StrongestConcepts,
    IReadOnlyList<ConceptProgressItem> WeakestConcepts,
    double ImprovementTrend);

public sealed record ConceptProgressItem(Guid ConceptId, string Name, int MasteryScore);
