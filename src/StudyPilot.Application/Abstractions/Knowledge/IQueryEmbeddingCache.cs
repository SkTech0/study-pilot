namespace StudyPilot.Application.Abstractions.Knowledge;

/// <summary>
/// Thread-safe cache for query embeddings to reduce cost and latency.
/// Key is derived from user query (e.g. hash); TTL 24 hours.
/// </summary>
public interface IQueryEmbeddingCache
{
    Task<float[]?> GetAsync(string userQuery, CancellationToken cancellationToken = default);
    Task SetAsync(string userQuery, float[] embedding, CancellationToken cancellationToken = default);
}
