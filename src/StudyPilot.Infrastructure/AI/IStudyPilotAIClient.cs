namespace StudyPilot.Infrastructure.AI;

public interface IStudyPilotAIClient
{
    Task<IReadOnlyList<ConceptDto>> ExtractConceptsAsync(Guid documentId, string text, CancellationToken ct = default);
    Task<GenerateQuizResultDto> GenerateQuizAsync(Guid documentId, IReadOnlyList<string> concepts, int questionCount, CancellationToken ct = default);
    Task<AIHealthStatus> CheckHealthAsync(CancellationToken ct = default);
}
