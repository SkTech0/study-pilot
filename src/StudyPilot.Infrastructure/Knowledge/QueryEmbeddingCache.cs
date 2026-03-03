using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using StudyPilot.Application.Abstractions.Caching;
using StudyPilot.Application.Abstractions.Knowledge;

namespace StudyPilot.Infrastructure.Knowledge;

public sealed class QueryEmbeddingCache : IQueryEmbeddingCache
{
    private const string KeyPrefix = "qe:";
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(24);

    private readonly ICacheService _cache;
    private readonly ILogger<QueryEmbeddingCache> _logger;

    public QueryEmbeddingCache(ICacheService cache, ILogger<QueryEmbeddingCache> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public Task<float[]?> GetAsync(string userQuery, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var key = KeyPrefix + GetCacheKey(userQuery);
        return _cache.GetAsync<float[]>(key, cancellationToken);
    }

    public Task SetAsync(string userQuery, float[] embedding, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var key = KeyPrefix + GetCacheKey(userQuery);
        return _cache.SetAsync(key, embedding, Ttl, cancellationToken);
    }

    private static string GetCacheKey(string userQuery)
    {
        if (string.IsNullOrWhiteSpace(userQuery))
            return string.Empty;
        var normalized = userQuery.Trim().ToLowerInvariant();
        var bytes = Encoding.UTF8.GetBytes(normalized);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
