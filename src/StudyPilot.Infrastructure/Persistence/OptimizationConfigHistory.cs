namespace StudyPilot.Infrastructure.Persistence;

public sealed class OptimizationConfigHistory
{
    public long Id { get; set; }
    public DateTime AppliedAtUtc { get; set; }
    public int ChunkSizeTokens { get; set; }
    public int VectorTopK { get; set; }
    public int EmbeddingBatchSize { get; set; }
    public int MaxAIConcurrency { get; set; }
    public int RetryBaseDelaySeconds { get; set; }
    public int Version { get; set; }
}
