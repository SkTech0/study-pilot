namespace StudyPilot.API.Contracts.Responses;

public sealed record WeakTopicResponse(Guid ConceptId, string Name, int MasteryScore);
