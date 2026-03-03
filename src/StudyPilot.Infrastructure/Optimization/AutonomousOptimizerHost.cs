using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StudyPilot.Application.Abstractions.Optimization;
using StudyPilot.Infrastructure.Services;

namespace StudyPilot.Infrastructure.Optimization;

public sealed class AutonomousOptimizerHost : BackgroundService
{
    private const int IntervalMinutes = 10;
    private readonly IServiceProvider _services;
    private readonly IOptimizationConfigProvider _configProvider;
    private readonly ILogger<AutonomousOptimizerHost> _logger;

    public AutonomousOptimizerHost(
        IServiceProvider services,
        IOptimizationConfigProvider configProvider,
        ILogger<AutonomousOptimizerHost> logger)
    {
        _services = services;
        _configProvider = configProvider;
        _logger = logger;
        StudyPilotMetrics.SetOptimizationVectorTopKProvider(() => _configProvider.GetVectorTopK());
        StudyPilotMetrics.SetOptimizationChunkSizeProvider(() => _configProvider.GetChunkSizeTokens());
        StudyPilotMetrics.SetOptimizationConcurrencyProvider(() => _configProvider.GetMaxAIConcurrency());
        StudyPilotMetrics.SetOptimizationFreezeStateProvider(() =>
        {
            using var scope = _services.CreateScope();
            return scope.ServiceProvider.GetRequiredService<IOptimizationSafetyGuard>().ShouldFreezeOptimization() ? 1 : 0;
        });
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(IntervalMinutes), stoppingToken);
                using var scope = _services.CreateScope();
                var optimizer = scope.ServiceProvider.GetRequiredService<IAutonomousOptimizer>();
                await optimizer.RunCycleAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AutonomousOptimizerHost cycle failed");
            }
        }
    }
}
