using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StudyPilot.Application.Abstractions.Optimization;

namespace StudyPilot.Infrastructure.Optimization;

public sealed class DatabaseOptimizationConfigProvider : IOptimizationConfigProvider
{
    private const int CacheSeconds = 30;
    private const string CacheKey = "optimization_config";
    private readonly IServiceProvider _services;
    private readonly IMemoryCache _cache;
    private readonly ILogger<DatabaseOptimizationConfigProvider> _logger;
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private volatile OptimizationConfigDto? _cached;

    private static readonly OptimizationConfigDto Defaults = new(
        ChunkSizeTokens: 800,
        VectorTopK: 24,
        EmbeddingBatchSize: 32,
        MaxAIConcurrency: 4,
        RetryBaseDelaySeconds: 5,
        LastUpdatedUtc: DateTime.UtcNow,
        Version: 1);

    public DatabaseOptimizationConfigProvider(
        IServiceProvider services,
        IMemoryCache cache,
        ILogger<DatabaseOptimizationConfigProvider> logger)
    {
        _services = services;
        _cache = cache;
        _logger = logger;
    }

    public int GetChunkSizeTokens() => _cached?.ChunkSizeTokens ?? Defaults.ChunkSizeTokens;
    public int GetVectorTopK() => _cached?.VectorTopK ?? Defaults.VectorTopK;
    public int GetEmbeddingBatchSize() => _cached?.EmbeddingBatchSize ?? Defaults.EmbeddingBatchSize;
    public int GetMaxAIConcurrency() => _cached?.MaxAIConcurrency ?? Defaults.MaxAIConcurrency;
    public int GetRetryBaseDelaySeconds() => _cached?.RetryBaseDelaySeconds ?? Defaults.RetryBaseDelaySeconds;

    public async Task<int> GetChunkSizeTokensAsync(CancellationToken cancellationToken = default) =>
        (await LoadConfigAsync(cancellationToken).ConfigureAwait(false)).ChunkSizeTokens;
    public async Task<int> GetVectorTopKAsync(CancellationToken cancellationToken = default) =>
        (await LoadConfigAsync(cancellationToken).ConfigureAwait(false)).VectorTopK;
    public async Task<int> GetEmbeddingBatchSizeAsync(CancellationToken cancellationToken = default) =>
        (await LoadConfigAsync(cancellationToken).ConfigureAwait(false)).EmbeddingBatchSize;
    public async Task<int> GetMaxAIConcurrencyAsync(CancellationToken cancellationToken = default) =>
        (await LoadConfigAsync(cancellationToken).ConfigureAwait(false)).MaxAIConcurrency;
    public async Task<int> GetRetryBaseDelaySecondsAsync(CancellationToken cancellationToken = default) =>
        (await LoadConfigAsync(cancellationToken).ConfigureAwait(false)).RetryBaseDelaySeconds;

    private async Task<OptimizationConfigDto> LoadConfigAsync(CancellationToken cancellationToken)
    {
        if (_cached is not null)
            return _cached;

        await _loadLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_cached is not null)
                return _cached;

            var fromCache = _cache.Get<OptimizationConfigDto>(CacheKey);
            if (fromCache is not null)
            {
                _cached = fromCache;
                return _cached;
            }

            try
            {
                await using var scope = _services.CreateAsyncScope();
                var repo = scope.ServiceProvider.GetRequiredService<IOptimizationConfigRepository>();
                var config = await repo.GetSingleAsync(cancellationToken).ConfigureAwait(false);
                var value = config ?? Defaults;
                _cache.Set(CacheKey, value, TimeSpan.FromSeconds(CacheSeconds));
                _cached = value;
                return value;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load optimization config; using defaults");
                _cached = Defaults;
                return Defaults;
            }
        }
        finally
        {
            _loadLock.Release();
        }
    }
}
