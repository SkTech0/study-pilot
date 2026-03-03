namespace StudyPilot.API.Contracts.Responses;

public sealed record LearningProgressResponse(
    IReadOnlyList<ConceptProgressItemResponse> StrongestConcepts,
    IReadOnlyList<ConceptProgressItemResponse> WeakestConcepts,
    double ImprovementTrend);

public sealed record ConceptProgressItemResponse(Guid ConceptId, string Name, int MasteryScore);
