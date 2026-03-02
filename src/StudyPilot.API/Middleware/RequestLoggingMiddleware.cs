using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using StudyPilot.Application.Abstractions.Observability;
using StudyPilot.Infrastructure.Services;

namespace StudyPilot.API.Middleware;

public sealed class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ICorrelationIdAccessor? correlationIdAccessor)
    {
        var path = context.Request.Path.Value ?? "";
        if (path.StartsWith("/health", StringComparison.OrdinalIgnoreCase) || path.Equals("/metrics", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }
        var sw = Stopwatch.StartNew();
        await _next(context);
        sw.Stop();
        var correlationId = correlationIdAccessor?.Get() ?? "";
        StudyPilotMetrics.HttpRequestsTotal.Add(1);
        StudyPilotMetrics.HttpRequestDurationMs.Record(sw.ElapsedMilliseconds);
        _logger.LogInformation("Request {Method} {Path} completed with {StatusCode} in {ElapsedMs}ms CorrelationId={CorrelationId}",
            context.Request.Method, path, context.Response.StatusCode, sw.ElapsedMilliseconds, correlationId);
    }
}
