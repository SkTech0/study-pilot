using System.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using StudyPilot.Application.Abstractions.BackgroundJobs;
using StudyPilot.Application.Abstractions.Knowledge;
using StudyPilot.Infrastructure.Persistence;
using StudyPilot.Infrastructure.Persistence.Repositories;

namespace StudyPilot.Infrastructure.BackgroundJobs;

public sealed class DbBackedKnowledgeEmbeddingJobQueue : IKnowledgeEmbeddingJobQueue
{
    private readonly IServiceProvider _services;
    private volatile int _pendingCountFromDb = -1;

    public int CachedPendingCount => _pendingCountFromDb >= 0 ? _pendingCountFromDb : 0;
    internal void SetPendingCountFromDb(int count) => _pendingCountFromDb = count;

    public DbBackedKnowledgeEmbeddingJobQueue(IServiceProvider services) => _services = services;

    public async Task EnqueueCreateEmbeddingsAsync(Guid documentId, string? correlationId, PipelinePriority priority = PipelinePriority.High, CancellationToken cancellationToken = default)
    {
        await using var scope = _services.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<IKnowledgeEmbeddingJobRepository>();
        var options = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<BackgroundJobOptions>>().Value;

        var job = new KnowledgeEmbeddingJob
        {
            Id = Guid.NewGuid(),
            DocumentId = documentId,
            CorrelationId = correlationId,
            Status = "Pending",
            RetryCount = 0,
            MaxRetries = Math.Max(1, options.MaxRetries),
            CreatedAtUtc = DateTime.UtcNow,
            Priority = (int)priority
        };

        try
        {
            await repo.AddAsync(job, cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Another node already enqueued a pending/processing job for this document (partial unique index).
        }
    }
}

