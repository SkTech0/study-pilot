using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StudyPilot.Application.Abstractions.AI;
using StudyPilot.Application.Abstractions.BackgroundJobs;
using StudyPilot.Application.Abstractions.Knowledge;
using StudyPilot.Infrastructure.Optimization;
using StudyPilot.Infrastructure.Persistence.Repositories;
using StudyPilot.Infrastructure.Services;

namespace StudyPilot.Infrastructure.BackgroundJobs;

public sealed class KnowledgeEmbeddingJobWorker : BackgroundService
{
    private const int PendingCountPollThrottle = 6;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DbBackedKnowledgeEmbeddingJobQueue _queue;
    private readonly BackgroundJobOptions _options;
    private readonly IKnowledgePipelineCoordinator _coordinator;
    private readonly IAIFailureClassifier _classifier;
    private readonly OptimizationMetricsBuffer? _metricsBuffer;
    private readonly ILogger<KnowledgeEmbeddingJobWorker> _logger;
    private int _pollCount;

    public KnowledgeEmbeddingJobWorker(
        IServiceScopeFactory scopeFactory,
        DbBackedKnowledgeEmbeddingJobQueue queue,
        IOptions<BackgroundJobOptions> options,
        IKnowledgePipelineCoordinator coordinator,
        IAIFailureClassifier classifier,
        ILogger<KnowledgeEmbeddingJobWorker> logger,
        OptimizationMetricsBuffer? metricsBuffer = null)
    {
        _scopeFactory = scopeFactory;
        _queue = queue;
        _options = options.Value;
        _coordinator = coordinator;
        _classifier = classifier;
        _metricsBuffer = metricsBuffer;
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

                if (_coordinator.GlobalMode == PipelineMode.Overloaded)
                {
                    var overloadDelay = TimeSpan.FromSeconds(pollInterval.TotalSeconds * 3);
                    _logger.LogDebug("KnowledgeEmbeddingJobWorker backing off pipeline_mode=Overloaded DelayMs={DelayMs}", overloadDelay.TotalMilliseconds);
                    await Task.Delay(overloadDelay, stoppingToken);
                    continue;
                }

                var job = await jobRepository.TryClaimNextAsync(workerId, processingTimeout, maxRetries, _coordinator.AllowLowPriorityJobs, stoppingToken);
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
                    _metricsBuffer?.RecordSuccess();
                    _logger.LogInformation("KnowledgeEmbeddingJobCompleted JobId={JobId} DocumentId={DocumentId} CorrelationId={CorrelationId}",
                        job.Id, job.DocumentId, job.CorrelationId);
                }
                catch (OperationCanceledException)
                {
                    var allowRetry = job.RetryCount + 1 < maxRetries;
                    var retryBaseSec = scope.ServiceProvider.GetService<StudyPilot.Application.Abstractions.Optimization.IOptimizationConfigProvider>()?.GetRetryBaseDelaySeconds() ?? 5;
                    var nextRetry = allowRetry ? DateTime.UtcNow.AddSeconds(Math.Pow(2, job.RetryCount) * retryBaseSec) : (DateTime?)null;
                    await jobRepository.MarkFailedAsync(job.Id, "Embedding job cancelled or timed out.", allowRetry, nextRetry, stoppingToken);
                    if (allowRetry) _metricsBuffer?.RecordRetry();
                    _logger.LogWarning("KnowledgeEmbeddingJobCancelled JobId={JobId} DocumentId={DocumentId} CorrelationId={CorrelationId} RetryCount={RetryCount}",
                        job.Id, job.DocumentId, job.CorrelationId, job.RetryCount);
                }
                catch (Exception ex)
                {
                    var classification = _classifier.Classify(ex, job.RetryCount, maxRetries);
                    var allowRetry = classification.AllowRetry;
                    var nextRetry = allowRetry ? DateTime.UtcNow.AddSeconds(classification.RetryDelaySeconds) : (DateTime?)null;
                    var failureReason = ex.Message?.Length > 1000 ? ex.Message[..1000] : (ex.Message ?? "Unknown error");
                    var limiter = scope.ServiceProvider.GetRequiredService<IAIExecutionLimiter>();
                    if (classification.OpenCircuit)
                        limiter.SetCircuitOpen(true);
                    await jobRepository.MarkFailedAsync(job.Id, failureReason, allowRetry, nextRetry, stoppingToken);
                    if (allowRetry)
                    {
                        _metricsBuffer?.RecordRetry();
                        StudyPilotMetrics.JobRetriesTotal.Add(1, new KeyValuePair<string, object?>("queue", "embedding"));
                    }
                    _logger.LogError(ex, "KnowledgeEmbeddingJobFailed JobId={JobId} DocumentId={DocumentId} CorrelationId={CorrelationId} RetryCount={RetryCount} Kind={Kind} instance_id={InstanceId} global_mode={GlobalMode} local_mode={LocalMode}",
                        job.Id, job.DocumentId, job.CorrelationId, job.RetryCount, classification.Kind, _coordinator.InstanceId, _coordinator.GlobalMode, _coordinator.LocalMode);
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

