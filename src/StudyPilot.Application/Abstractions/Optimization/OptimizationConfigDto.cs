namespace StudyPilot.Application.Abstractions.Optimization;

public sealed record OptimizationConfigDto(
    int ChunkSizeTokens,
    int VectorTopK,
    int EmbeddingBatchSize,
    int MaxAIConcurrency,
    int RetryBaseDelaySeconds,
    DateTime LastUpdatedUtc,
    int Version);
