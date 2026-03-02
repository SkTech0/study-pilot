using StudyPilot.Application.Common.Models;

namespace StudyPilot.Application.Abstractions.AI;

public interface IAIService
{
    Task<ExtractConceptsResult> ExtractConceptsAsync(Guid documentId, string contentOrPath, CancellationToken cancellationToken = default);
    Task<GenerateQuizResult> GenerateQuizAsync(Guid documentId, Guid userId, IReadOnlyList<ConceptInfo> concepts, CancellationToken cancellationToken = default);
    /// <summary>Generate a single question for one concept (e.g. for lazy per-index generation).</summary>
    Task<GeneratedQuestion?> GenerateQuestionAsync(Guid documentId, Guid userId, ConceptInfo concept, CancellationToken cancellationToken = default);
}
