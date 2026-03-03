using StudyPilot.Application.Knowledge.Models;

namespace StudyPilot.Application.Abstractions.Knowledge;

/// <summary>
/// Hybrid retrieval: vector + keyword search, merge and rerank.
/// Falls back to vector-only if keyword search fails.
/// </summary>
public interface IHybridSearchService
{
    Task<IReadOnlyList<RetrievedChunk>> SearchAsync(
        Guid userId,
        float[] queryEmbedding,
        Guid? documentId,
        string queryText,
        int topK,
        CancellationToken cancellationToken = default);
}
