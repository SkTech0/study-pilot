using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StudyPilot.Infrastructure.AI;

namespace StudyPilot.Infrastructure.Hosting;

public sealed class AIStartupCheck : IHostedService
{
    private const int MaxRetries = 3;
    private readonly IServiceProvider _services;
    private readonly ILogger<AIStartupCheck> _logger;

    public AIStartupCheck(IServiceProvider services, ILogger<AIStartupCheck> logger)
    {
        _services = services;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                using var scope = _services.CreateScope();
                var client = scope.ServiceProvider.GetRequiredService<IStudyPilotAIClient>();
                var status = await client.CheckHealthAsync(cancellationToken);
                if (status != AIHealthStatus.Unhealthy)
                {
                    _logger.LogInformation("AI startup check passed: {Status}", status);
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AI startup check attempt {Attempt} failed", attempt);
            }
            if (attempt < MaxRetries)
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
        }
        _logger.LogWarning("AI service unavailable at startup; API will run in degraded mode");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
