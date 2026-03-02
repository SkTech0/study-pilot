using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace StudyPilot.Infrastructure.Hosting;

public interface IWorkerHeartbeat
{
    DateTime LastHeartbeatUtc { get; }
    bool IsAlive { get; }
}

public sealed class WorkerHeartbeatStore : IWorkerHeartbeat
{
    private DateTime _lastHeartbeatUtc = DateTime.UtcNow;
    private const double StaleSeconds = 60;

    public DateTime LastHeartbeatUtc => _lastHeartbeatUtc;
    public bool IsAlive => (DateTime.UtcNow - _lastHeartbeatUtc).TotalSeconds < StaleSeconds;
    public void Update() => _lastHeartbeatUtc = DateTime.UtcNow;
}

public sealed class WorkerHeartbeatService : BackgroundService
{
    private readonly WorkerHeartbeatStore _store;
    private const int IntervalSeconds = 30;

    public WorkerHeartbeatService(WorkerHeartbeatStore store) => _store = store;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _store.Update();
            await Task.Delay(TimeSpan.FromSeconds(IntervalSeconds), stoppingToken);
        }
    }
}
