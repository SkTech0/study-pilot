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

    public int GetChunkSizeTokens() => GetConfig().ChunkSizeTokens;
    public int GetVectorTopK() => GetConfig().VectorTopK;
    public int GetEmbeddingBatchSize() => GetConfig().EmbeddingBatchSize;
    public int GetMaxAIConcurrency() => GetConfig().MaxAIConcurrency;
    public int GetRetryBaseDelaySeconds() => GetConfig().RetryBaseDelaySeconds;

    private OptimizationConfigDto GetConfig()
    {
        return _cache.GetOrCreate(CacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(CacheSeconds);
            try
            {
                using var scope = _services.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<IOptimizationConfigRepository>();
                var config = repo.GetSingleAsync().GetAwaiter().GetResult();
                return config ?? Defaults;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load optimization config; using defaults");
                return Defaults;
            }
        }) ?? Defaults;
    }
}
