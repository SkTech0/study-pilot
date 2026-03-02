using StudyPilot.Application.Knowledge.Models;

namespace StudyPilot.Application.Abstractions.Knowledge;

public interface IVectorSearchService
{
    Task<IReadOnlyList<RetrievedChunk>> SearchAsync(Guid userId, float[] queryEmbedding, Guid? documentId, int topK, CancellationToken cancellationToken = default);
}

