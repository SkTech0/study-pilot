using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace StudyPilot.Infrastructure.Hosting;

public sealed class DatabaseMigrationHostedService : IHostedService
{
    private readonly ILogger<DatabaseMigrationHostedService> _logger;

    public DatabaseMigrationHostedService(ILogger<DatabaseMigrationHostedService> logger)
    {
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Migrations are run in Program.cs before the host starts so the schema exists before any background workers run.
        _logger.LogDebug("Database migrations are applied at application startup.");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
