using StudyPilot.Application.Abstractions.Knowledge;
using StudyPilot.Domain.Entities;
using StudyPilot.Infrastructure.AI;

namespace StudyPilot.Infrastructure.Knowledge;

public sealed class EmbeddingService : IEmbeddingService
{
    private readonly IStudyPilotKnowledgeAIClient _client;

    public EmbeddingService(IStudyPilotKnowledgeAIClient client) => _client = client;

    public async Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        var list = await EmbedBatchAsync(new[] { text }, cancellationToken);
        return list.Count > 0 ? list[0] : new float[DocumentChunk.EmbeddingDimensions];
    }

    public async Task<IReadOnlyList<float[]>> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
    {
        if (texts.Count == 0) return Array.Empty<float[]>();
        var result = await _client.CreateEmbeddingsAsync(texts, cancellationToken);
        if (result.Embeddings.Count != texts.Count)
            throw new InvalidOperationException($"Embedding count mismatch: expected {texts.Count}, got {result.Embeddings.Count}.");
        foreach (var emb in result.Embeddings)
        {
            if (emb.Length != DocumentChunk.EmbeddingDimensions)
                throw new InvalidOperationException($"Embedding dimension mismatch: expected {DocumentChunk.EmbeddingDimensions}, got {emb.Length}.");
        }
        return result.Embeddings;
    }
}

