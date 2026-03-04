namespace StudyPilot.Application.Abstractions.Optimization;

/// <summary>
/// Provides runtime optimization parameters. Values are cached and read from the database.
/// Use async methods where possible; sync methods return cached value (or defaults if cache cold).
/// </summary>
public interface IOptimizationConfigProvider
{
    int GetChunkSizeTokens();
    int GetVectorTopK();
    int GetEmbeddingBatchSize();
    int GetMaxAIConcurrency();
    int GetRetryBaseDelaySeconds();

    Task<int> GetChunkSizeTokensAsync(CancellationToken cancellationToken = default);
    Task<int> GetVectorTopKAsync(CancellationToken cancellationToken = default);
    Task<int> GetEmbeddingBatchSizeAsync(CancellationToken cancellationToken = default);
    Task<int> GetMaxAIConcurrencyAsync(CancellationToken cancellationToken = default);
    Task<int> GetRetryBaseDelaySecondsAsync(CancellationToken cancellationToken = default);
}
