using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace StudyPilot.Infrastructure.Hosting;

public sealed class LaunchValidationHostedService : IHostedService
{
    private readonly ILogger<LaunchValidationHostedService> _logger;

    public LaunchValidationHostedService(ILogger<LaunchValidationHostedService> logger) => _logger = logger;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("STUDYPILOT READY FOR LAUNCH");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
