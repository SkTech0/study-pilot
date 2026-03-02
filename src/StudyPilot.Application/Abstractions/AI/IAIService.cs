using StudyPilot.Application.Common.Models;

namespace StudyPilot.Application.Abstractions.AI;

public interface IAIService
{
    Task<ExtractConceptsResult> ExtractConceptsAsync(Guid documentId, string contentOrPath, CancellationToken cancellationToken = default);
    Task<GenerateQuizResult> GenerateQuizAsync(Guid documentId, Guid userId, IReadOnlyList<ConceptInfo> concepts, CancellationToken cancellationToken = default);
}
