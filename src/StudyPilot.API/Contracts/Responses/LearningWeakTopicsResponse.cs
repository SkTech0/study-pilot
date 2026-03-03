namespace StudyPilot.API.Contracts.Responses;

public sealed record LearningWeakTopicsResponse(IReadOnlyList<WeakTopicItemResponse> Topics);

public sealed record WeakTopicItemResponse(Guid ConceptId, string ConceptName, int MasteryScore);
