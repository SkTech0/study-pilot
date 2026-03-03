using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using StudyPilot.Infrastructure.Resilience;

namespace StudyPilot.API.Middleware;

public sealed class ChaosSimulationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ChaosSimulationOptions _options;

    public ChaosSimulationMiddleware(RequestDelegate next, IOptions<ChaosSimulationOptions> options)
    {
        _next = next;
        _options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (_options.SimulateDBDelay && _options.SimulateDBDelayMs > 0)
            await Task.Delay(_options.SimulateDBDelayMs, context.RequestAborted).ConfigureAwait(false);
        await _next(context).ConfigureAwait(false);
    }
}
