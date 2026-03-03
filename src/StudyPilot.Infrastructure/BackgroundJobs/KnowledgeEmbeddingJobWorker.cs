using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StudyPilot.Application.Abstractions.BackgroundJobs;
using StudyPilot.Infrastructure.Persistence.Repositories;
using StudyPilot.Infrastructure.Services;

namespace StudyPilot.Infrastructure.BackgroundJobs;

public sealed class KnowledgeEmbeddingJobWorker : BackgroundService
{
    private const int PendingCountPollThrottle = 6;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DbBackedKnowledgeEmbeddingJobQueue _queue;
    private readonly BackgroundJobOptions _options;
    private readonly ILogger<KnowledgeEmbeddingJobWorker> _logger;
    private int _pollCount;

    public KnowledgeEmbeddingJobWorker(
        IServiceScopeFactory scopeFactory,
        DbBackedKnowledgeEmbeddingJobQueue queue,
        IOptions<BackgroundJobOptions> options,
        ILogger<KnowledgeEmbeddingJobWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _queue = queue;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var workerId = string.IsNullOrEmpty(_options.WorkerId) ? $"emb-{Guid.NewGuid():N}"[..20] : $"emb-{_options.WorkerId}";
        _logger.LogInformation("KnowledgeEmbeddingJobWorker started WorkerId={WorkerId}", workerId);
        var processingTimeout = TimeSpan.FromMinutes(Math.Max(1, _options.ProcessingTimeoutMinutes));
        var pollInterval = TimeSpan.FromSeconds(Math.Max(1, _options.PollIntervalSeconds));
        var maxRetries = Math.Max(1, _options.MaxRetries);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var jobRepository = scope.ServiceProvider.GetRequiredService<IKnowledgeEmbeddingJobRepository>();
                var jobFactory = scope.ServiceProvider.GetRequiredService<IKnowledgeEmbeddingJobFactory>();

                await jobRepository.ReleaseStuckJobsAsync(processingTimeout, stoppingToken);

                var job = await jobRepository.TryClaimNextAsync(workerId, processingTimeout, maxRetries, stoppingToken);
                if (job is null)
                {
                    if (Interlocked.Increment(ref _pollCount) % PendingCountPollThrottle == 0)
                        _queue.SetPendingCountFromDb(await jobRepository.GetPendingCountAsync(stoppingToken));
                    await Task.Delay(pollInterval, stoppingToken);
                    continue;
                }

                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                timeoutCts.CancelAfter(processingTimeout);

                try
                {
                    var runJob = jobFactory.CreateEmbeddingJob(job.DocumentId, job.CorrelationId);
                    await runJob(timeoutCts.Token);
                    await jobRepository.MarkCompletedAsync(job.Id, stoppingToken);
                    _logger.LogInformation("KnowledgeEmbeddingJobCompleted JobId={JobId} DocumentId={DocumentId} CorrelationId={CorrelationId}",
                        job.Id, job.DocumentId, job.CorrelationId);
                }
                catch (OperationCanceledException)
                {
                    var allowRetry = job.RetryCount + 1 < maxRetries;
                    var nextRetry = allowRetry ? DateTime.UtcNow.AddSeconds(Math.Pow(2, job.RetryCount) * 5) : (DateTime?)null;
                    await jobRepository.MarkFailedAsync(job.Id, "Embedding job cancelled or timed out.", allowRetry, nextRetry, stoppingToken);
                    _logger.LogWarning("KnowledgeEmbeddingJobCancelled JobId={JobId} DocumentId={DocumentId} CorrelationId={CorrelationId} RetryCount={RetryCount}",
                        job.Id, job.DocumentId, job.CorrelationId, job.RetryCount);
                }
                catch (Exception ex)
                {
                    var isTransient = ex is HttpRequestException or TimeoutException or OperationCanceledException;
                    var allowRetry = isTransient && job.RetryCount + 1 < maxRetries;
                    var nextRetry = allowRetry ? DateTime.UtcNow.AddSeconds(Math.Pow(2, job.RetryCount) * 5) : (DateTime?)null;
                    var failureReason = ex.Message?.Length > 1000 ? ex.Message[..1000] : (ex.Message ?? "Unknown error");
                    await jobRepository.MarkFailedAsync(job.Id, failureReason, allowRetry, nextRetry, stoppingToken);
                    if (allowRetry)
                        StudyPilotMetrics.JobRetriesTotal.Add(1, new KeyValuePair<string, object?>("queue", "embedding"));
                    _logger.LogError(ex, "KnowledgeEmbeddingJobFailed JobId={JobId} DocumentId={DocumentId} CorrelationId={CorrelationId} RetryCount={RetryCount} Poison={Poison}",
                        job.Id, job.DocumentId, job.CorrelationId, job.RetryCount, !allowRetry);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "KnowledgeEmbeddingJobWorker poll or claim error");
                await Task.Delay(pollInterval, stoppingToken);
            }
        }

        _logger.LogInformation("KnowledgeEmbeddingJobWorker stopped");
    }
}

