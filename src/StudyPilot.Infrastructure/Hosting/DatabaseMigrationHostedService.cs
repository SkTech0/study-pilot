using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StudyPilot.Infrastructure.Persistence.DbContext;

namespace StudyPilot.Infrastructure.Hosting;

public sealed class DatabaseMigrationHostedService : IHostedService
{
    private const int MaxRetries = 3;
    private const int DelayMs = 2000;

    private readonly IServiceProvider _services;
    private readonly ILogger<DatabaseMigrationHostedService> _logger;

    public DatabaseMigrationHostedService(IServiceProvider services, ILogger<DatabaseMigrationHostedService> logger)
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
                await db.Database.MigrateAsync(cancellationToken);
                _logger.LogInformation("Database migration completed");
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Database migration attempt {Attempt} failed", attempt);
                if (attempt == MaxRetries)
                {
                    _logger.LogError("Database migration failed after {MaxRetries} attempts", MaxRetries);
                    throw;
                }
                await Task.Delay(DelayMs, cancellationToken);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
