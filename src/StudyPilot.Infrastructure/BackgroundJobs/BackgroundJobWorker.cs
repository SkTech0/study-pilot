using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StudyPilot.Infrastructure.Services;

namespace StudyPilot.Infrastructure.BackgroundJobs;

public sealed class BackgroundJobWorker : BackgroundService
{
    private readonly InMemoryBackgroundJobQueue _queue;
    private readonly IBackgroundQueueMetrics _metrics;
    private readonly ILogger<BackgroundJobWorker> _logger;

    public BackgroundJobWorker(
        InMemoryBackgroundJobQueue queue,
        IBackgroundQueueMetrics metrics,
        ILogger<BackgroundJobWorker> logger)
    {
        _queue = queue;
        _metrics = metrics;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BackgroundJobWorker started");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var job = await _queue.DequeueAsync(stoppingToken);
                StudyPilotMetrics.BackgroundJobsTotal.Add(1);
                try
                {
                    await job(stoppingToken);
                    _metrics.RecordProcessed();
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    StudyPilotMetrics.BackgroundJobFailuresTotal.Add(1);
                    _logger.LogError(ex, "Background job failed");
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
                when (ex is not OperationCanceledException)
            {
                StudyPilotMetrics.BackgroundJobFailuresTotal.Add(1);
                _logger.LogError(ex, "BackgroundJobWorker dequeue or execution error");
            }
        }
        _logger.LogInformation("BackgroundJobWorker stopped");
    }
}
