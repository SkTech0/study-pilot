using System.Diagnostics.Metrics;
using System.Net;
using Microsoft.Extensions.Logging;
using StudyPilot.Application.Abstractions.AI;
using StudyPilot.Application.Abstractions.Optimization;
using StudyPilot.Infrastructure.Services;

namespace StudyPilot.Infrastructure.AI;

public sealed class AIFailureClassifier : IAIFailureClassifier
{
    private readonly ILogger<AIFailureClassifier>? _logger;
    private readonly IOptimizationConfigProvider _configProvider;
    private readonly Meter _meter;
    private readonly Counter<long> _classificationsCounter;

    private static readonly string[] RateLimitPhrases = { "rate limit", "429", "too many requests", "quota", "resource exhausted", "overloaded" };
    private static readonly string[] ProviderDownPhrases = { "503", "502", "501", "service unavailable", "bad gateway", "connection refused", "connection reset", "timeout" };
    private static readonly string[] PermanentPhrases = { "400", "invalid request", "authentication failed", "401", "403", "forbidden" };
    private static readonly string[] InvalidInputPhrases = { "invalid input", "validation", "400 bad request", "malformed" };

    public AIFailureClassifier(IOptimizationConfigProvider configProvider, ILogger<AIFailureClassifier>? logger = null)
    {
        _configProvider = configProvider;
        _logger = logger;
        _meter = new Meter(StudyPilotMetrics.MeterName, "1.0");
        _classificationsCounter = _meter.CreateCounter<long>("knowledge_retry_classifications");
    }

    public AIFailureClassificationResult Classify(Exception exception, int currentRetryCount, int maxRetries)
    {
        var message = (exception?.Message ?? "").ToLowerInvariant();
        var inner = exception?.InnerException?.Message?.ToLowerInvariant() ?? "";
        var baseSec = Math.Max(1, _configProvider.GetRetryBaseDelaySeconds());

        if (IsInvalidInput(message, inner))
        {
            _classificationsCounter.Add(1, new KeyValuePair<string, object?>("kind", "InvalidInput"));
            return new AIFailureClassificationResult
            {
                Kind = AIFailureKind.InvalidInput,
                AllowRetry = false,
                RetryDelaySeconds = 0,
                OpenCircuit = false,
                StopPipelineSafely = true
            };
        }

        if (IsRateLimit(message, inner))
        {
            _classificationsCounter.Add(1, new KeyValuePair<string, object?>("kind", "RateLimit"));
            var delay = Math.Min(baseSec * 12 * Math.Pow(2, currentRetryCount), 600);
            return new AIFailureClassificationResult
            {
                Kind = AIFailureKind.RateLimit,
                AllowRetry = currentRetryCount < maxRetries,
                RetryDelaySeconds = delay,
                OpenCircuit = false,
                StopPipelineSafely = false
            };
        }

        if (IsProviderDown(exception, message, inner))
        {
            _classificationsCounter.Add(1, new KeyValuePair<string, object?>("kind", "ProviderDown"));
            return new AIFailureClassificationResult
            {
                Kind = AIFailureKind.ProviderDown,
                AllowRetry = currentRetryCount < maxRetries,
                RetryDelaySeconds = Math.Min(baseSec * 6 * Math.Pow(2, currentRetryCount), 300),
                OpenCircuit = true,
                StopPipelineSafely = false
            };
        }

        if (IsPermanent(message, inner))
        {
            _classificationsCounter.Add(1, new KeyValuePair<string, object?>("kind", "Permanent"));
            return new AIFailureClassificationResult
            {
                Kind = AIFailureKind.Permanent,
                AllowRetry = false,
                RetryDelaySeconds = 0,
                OpenCircuit = false,
                StopPipelineSafely = true
            };
        }

        _classificationsCounter.Add(1, new KeyValuePair<string, object?>("kind", "Transient"));
        return new AIFailureClassificationResult
        {
            Kind = AIFailureKind.Transient,
            AllowRetry = currentRetryCount < maxRetries,
            RetryDelaySeconds = Math.Min(baseSec * Math.Pow(2, currentRetryCount), 120),
            OpenCircuit = false,
            StopPipelineSafely = false
        };
    }

    private static bool IsInvalidInput(string message, string inner)
    {
        return InvalidInputPhrases.Any(p => message.Contains(p) || inner.Contains(p));
    }

    private static bool IsRateLimit(string message, string inner)
    {
        var combined = message + " " + inner;
        return RateLimitPhrases.Any(p => combined.Contains(p));
    }

    private static bool IsProviderDown(Exception? ex, string message, string inner)
    {
        if (ex is HttpRequestException or TaskCanceledException or TimeoutException)
            return true;
        return ProviderDownPhrases.Any(p => message.Contains(p) || inner.Contains(p));
    }

    private static bool IsPermanent(string message, string inner)
    {
        return PermanentPhrases.Any(p => message.Contains(p) || inner.Contains(p));
    }
}
