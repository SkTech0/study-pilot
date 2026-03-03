namespace StudyPilot.Application.Abstractions.Optimization;

/// <summary>
/// Provides runtime optimization parameters. Values are cached and read from the database.
/// All optimization-aware services should use this instead of hardcoded constants.
/// </summary>
public interface IOptimizationConfigProvider
{
    int GetChunkSizeTokens();
    int GetVectorTopK();
    int GetEmbeddingBatchSize();
    int GetMaxAIConcurrency();
    int GetRetryBaseDelaySeconds();
}
