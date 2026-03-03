namespace StudyPilot.Infrastructure.Persistence;

public sealed class OptimizationConfig
{
    public int Id { get; set; }
    public int ChunkSizeTokens { get; set; }
    public int VectorTopK { get; set; }
    public int EmbeddingBatchSize { get; set; }
    public int MaxAIConcurrency { get; set; }
    public int RetryBaseDelaySeconds { get; set; }
    public DateTime LastUpdatedUtc { get; set; }
    public int Version { get; set; }
}
