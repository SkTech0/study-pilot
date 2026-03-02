using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
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
            catch (PostgresException pgEx) when (pgEx.SqlState == "42501")
            {
                var fix = "Run as postgres: psql -U postgres -d StudyPilot -f scripts/postgres-grant-public.sql (or see Prompts/RUN.md).";
                _logger.LogError("Database migration failed: permission denied for schema public. {Fix}", fix);
                throw new InvalidOperationException(
                    "Permission denied for schema public. PostgreSQL 15+ requires explicit grants. " + fix,
                    pgEx);
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
