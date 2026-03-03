using MediatR;
using StudyPilot.Application.Common.Models;

namespace StudyPilot.Application.Learning.GetLearningWeakTopics;

public sealed record GetLearningWeakTopicsQuery(Guid UserId, int MaxCount = 20) : IRequest<Result<LearningWeakTopicsResult>>;

public sealed record LearningWeakTopicsResult(IReadOnlyList<WeakTopicItem> Topics);

public sealed record WeakTopicItem(Guid ConceptId, string ConceptName, int MasteryScore);
