using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StudyPilot.Application.Abstractions.BackgroundJobs;
using StudyPilot.Application.Abstractions.Persistence;
using StudyPilot.Infrastructure.Persistence.Repositories;
using StudyPilot.Infrastructure.Services;

namespace StudyPilot.Infrastructure.BackgroundJobs;

public sealed class BackgroundJobWorker : BackgroundService
{
    private const int RecoveryLoopInterval = 12;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DbBackedBackgroundJobQueue _queue;
    private readonly IBackgroundJobQueue _jobQueue;
    private readonly BackgroundJobOptions _options;
    private readonly ILogger<BackgroundJobWorker> _logger;
    private int _loopCount;

    public BackgroundJobWorker(
        IServiceScopeFactory scopeFactory,
        DbBackedBackgroundJobQueue queue,
        IBackgroundJobQueue jobQueue,
        IOptions<BackgroundJobOptions> options,
        ILogger<BackgroundJobWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _queue = queue;
        _jobQueue = jobQueue;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var workerId = string.IsNullOrEmpty(_options.WorkerId) ? Guid.NewGuid().ToString("N")[..16] : _options.WorkerId;
        _logger.LogInformation("BackgroundJobWorker started WorkerId={WorkerId}", workerId);
        var processingTimeout = TimeSpan.FromMinutes(Math.Max(1, _options.ProcessingTimeoutMinutes));
        var pollInterval = TimeSpan.FromSeconds(Math.Max(1, _options.PollIntervalSeconds));
        var maxRetries = Math.Max(1, _options.MaxRetries);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var jobRepository = scope.ServiceProvider.GetRequiredService<IBackgroundJobRepository>();
                var documentRepository = scope.ServiceProvider.GetRequiredService<IDocumentRepository>();
                var jobFactory = scope.ServiceProvider.GetRequiredService<IDocumentProcessingJobFactory>();

                await jobRepository.ReleaseStuckJobsAsync(processingTimeout, stoppingToken);

                if (Interlocked.Increment(ref _loopCount) % RecoveryLoopInterval == 0)
                {
                    var cutoff = DateTime.UtcNow.Add(-processingTimeout);
                    var stuckIds = await documentRepository.GetStuckProcessingDocumentIdsAsync(cutoff, stoppingToken);
                    foreach (var docId in stuckIds)
                    {
                        if (await jobRepository.ExistsPendingOrProcessingForDocumentAsync(docId, stoppingToken))
                            continue;
                        await documentRepository.ResetToPendingAsync(docId, stoppingToken);
                        await _jobQueue.EnqueueDocumentProcessingAsync(docId, null, stoppingToken);
                        _logger.LogInformation("Stuck document recovered DocumentId={DocumentId}", docId);
                    }
                }

                var job = await jobRepository.TryClaimNextAsync(workerId, processingTimeout, maxRetries, stoppingToken);
                if (job is null)
                {
                    await Task.Delay(pollInterval, stoppingToken);
                    continue;
                }

                StudyPilotMetrics.BackgroundJobsTotal.Add(1);
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                timeoutCts.CancelAfter(processingTimeout);

                try
                {
                    var runJob = jobFactory.CreateProcessDocumentJob(job.DocumentId, job.CorrelationId);
                    await runJob(timeoutCts.Token);
                    await jobRepository.MarkCompletedAsync(job.Id, stoppingToken);
                    _queue.RecordProcessed();
                }
                catch (OperationCanceledException)
                {
                    var allowRetry = job.RetryCount + 1 < maxRetries;
                    var nextRetry = allowRetry ? DateTime.UtcNow.AddSeconds(Math.Pow(2, job.RetryCount) * 5) : (DateTime?)null;
                    await jobRepository.MarkFailedAsync(job.Id, "Processing cancelled or timed out.", allowRetry, nextRetry, stoppingToken);
                    if (allowRetry)
                        await documentRepository.ResetToPendingAsync(job.DocumentId, stoppingToken);
                    StudyPilotMetrics.BackgroundJobFailuresTotal.Add(1);
                    _logger.LogWarning("BackgroundJobCancelled JobId={JobId} DocumentId={DocumentId} CorrelationId={CorrelationId}",
                        job.Id, job.DocumentId, job.CorrelationId);
                }
                catch (Exception ex)
                {
                    var allowRetry = job.RetryCount + 1 < maxRetries;
                    var nextRetry = allowRetry ? DateTime.UtcNow.AddSeconds(Math.Pow(2, job.RetryCount) * 5) : (DateTime?)null;
                    await jobRepository.MarkFailedAsync(job.Id, ex.Message, allowRetry, nextRetry, stoppingToken);
                    if (allowRetry)
                        await documentRepository.ResetToPendingAsync(job.DocumentId, stoppingToken);
                    StudyPilotMetrics.BackgroundJobFailuresTotal.Add(1);
                    _logger.LogError(ex, "BackgroundJobFailed JobId={JobId} DocumentId={DocumentId} CorrelationId={CorrelationId} RetryCount={RetryCount}",
                        job.Id, job.DocumentId, job.CorrelationId, job.RetryCount);
                }
                finally
                {
                    _queue.DecrementPendingCount();
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                StudyPilotMetrics.BackgroundJobFailuresTotal.Add(1);
                _logger.LogError(ex, "BackgroundJobWorker poll or claim error");
                await Task.Delay(pollInterval, stoppingToken);
            }
        }

        _logger.LogInformation("BackgroundJobWorker stopped");
    }
}
