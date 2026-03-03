using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StudyPilot.Application.Abstractions.BackgroundJobs;
using StudyPilot.Infrastructure.Persistence;
using StudyPilot.Infrastructure.Persistence.Repositories;

namespace StudyPilot.Infrastructure.BackgroundJobs;

public sealed class DbBackedBackgroundJobQueue : IBackgroundJobQueue, IBackgroundQueueMetrics
{
    private readonly IServiceProvider _services;
    private readonly ILogger<DbBackedBackgroundJobQueue>? _logger;
    private int _pendingCountApprox;
    private volatile int _pendingCountFromDb = -1;
    private long _processedCount;

    public DbBackedBackgroundJobQueue(IServiceProvider services, ILogger<DbBackedBackgroundJobQueue>? logger = null)
    {
        _services = services;
        _logger = logger;
    }

    public int QueuedCount => _pendingCountFromDb >= 0 ? _pendingCountFromDb : Math.Max(0, _pendingCountApprox);

    internal void SetPendingCountFromDb(int count) => _pendingCountFromDb = count;
    public long ProcessedCount => _processedCount;

    public void RecordProcessed() => Interlocked.Increment(ref _processedCount);

    public async Task EnqueueDocumentProcessingAsync(Guid documentId, string? correlationId, CancellationToken cancellationToken = default)
    {
        await using var scope = _services.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<IBackgroundJobRepository>();
        var options = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<BackgroundJobOptions>>().Value;
        var job = new BackgroundJob
        {
            Id = Guid.NewGuid(),
            DocumentId = documentId,
            CorrelationId = correlationId,
            Status = "Pending",
            RetryCount = 0,
            MaxRetries = Math.Max(1, options.MaxRetries),
            CreatedAtUtc = DateTime.UtcNow
        };
        await repo.AddAsync(job, cancellationToken);
        Interlocked.Increment(ref _pendingCountApprox);
        _logger?.LogInformation("DocumentProcessingJobEnqueued JobId={JobId} DocumentId={DocumentId} CorrelationId={CorrelationId}",
            job.Id, documentId, correlationId);
    }

    internal void DecrementPendingCount() => Interlocked.Decrement(ref _pendingCountApprox);
}
