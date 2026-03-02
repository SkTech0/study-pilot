using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StudyPilot.Infrastructure.Persistence.DbContext;

namespace StudyPilot.Infrastructure.Hosting;

public sealed class DatabaseStartupCheck : IHostedService
{
    private const int MaxRetries = 5;
    private readonly IServiceProvider _services;
    private readonly ILogger<DatabaseStartupCheck> _logger;

    public DatabaseStartupCheck(IServiceProvider services, ILogger<DatabaseStartupCheck> logger)
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
                var db = scope.ServiceProvider.GetRequiredService<StudyPilotDbContext>();
                await db.Database.CanConnectAsync(cancellationToken);
                _logger.LogInformation("Database startup check passed");
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Database startup check attempt {Attempt} failed", attempt);
                if (attempt == MaxRetries)
                {
                    _logger.LogCritical("Database unreachable after {MaxRetries} attempts", MaxRetries);
                    throw new HostAbortedException("Database unreachable", ex);
                }
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                await Task.Delay(delay, cancellationToken);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
