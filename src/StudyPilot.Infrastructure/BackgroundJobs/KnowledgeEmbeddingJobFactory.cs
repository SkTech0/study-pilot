using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog.Context;
using StudyPilot.Application.Abstractions.BackgroundJobs;
using StudyPilot.Application.Abstractions.Knowledge;
using StudyPilot.Application.Abstractions.Observability;
using StudyPilot.Application.Abstractions.Persistence;
using StudyPilot.Domain.Entities;
using StudyPilot.Infrastructure.AI;
using StudyPilot.Infrastructure.Knowledge.Chunking;
using StudyPilot.Infrastructure.Storage;

namespace StudyPilot.Infrastructure.BackgroundJobs;

public sealed class KnowledgeEmbeddingJobFactory : IKnowledgeEmbeddingJobFactory
{
    private readonly IServiceProvider _services;

    public KnowledgeEmbeddingJobFactory(IServiceProvider services) => _services = services;

    public Func<CancellationToken, Task> CreateEmbeddingJob(Guid documentId, string? correlationId = null)
    {
        return async (ct) =>
        {
            await using var scope = _services.CreateAsyncScope();
            var documentRepo = scope.ServiceProvider.GetRequiredService<IDocumentRepository>();
            var chunkRepo = scope.ServiceProvider.GetRequiredService<IDocumentChunkRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var embeddingService = scope.ServiceProvider.GetRequiredService<IEmbeddingService>();
            var fileReader = scope.ServiceProvider.GetRequiredService<IFileContentReader>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<KnowledgeEmbeddingJobFactory>>();
            var correlationAccessor = scope.ServiceProvider.GetService<ICorrelationIdAccessor>();
            var aiOptions = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<AIServiceOptions>>().Value;

            if (!string.IsNullOrEmpty(correlationId))
                correlationAccessor?.Set(correlationId);

            using (LogContext.PushProperty("CorrelationId", correlationId ?? ""))
            {
                var doc = await documentRepo.GetByIdAsync(documentId, ct);
                if (doc is null) return;

                var sw = Stopwatch.StartNew();
                logger.LogInformation("KnowledgeEmbeddingStarted DocumentId={DocumentId} UserId={UserId} CorrelationId={CorrelationId}", documentId, doc.UserId, correlationId);

                var text = await fileReader.ReadAllTextAsync(doc.StoragePath, ct);
                if (text.Length > aiOptions.MaxTextLength)
                    text = text[..aiOptions.MaxTextLength];

                var chunks = TextChunker.Chunk(text, targetTokens: 800, overlapTokens: 150);
                logger.LogInformation("KnowledgeChunkingCompleted DocumentId={DocumentId} ChunkCount={ChunkCount} ElapsedMilliseconds={ElapsedMilliseconds}",
                    documentId, chunks.Count, sw.ElapsedMilliseconds);

                // Idempotency: remove any previous chunks before inserting.
                await chunkRepo.DeleteByDocumentIdAsync(documentId, ct);
                await unitOfWork.SaveChangesAsync(ct);

                const int batchSize = 32;
                var inserted = 0;
                for (var i = 0; i < chunks.Count; i += batchSize)
                {
                    var batch = chunks.Skip(i).Take(batchSize).ToList();
                    var texts = batch.Select(b => b.Text).ToList();
                    var embeddings = await embeddingService.EmbedBatchAsync(texts, ct);

                    var entities = new List<DocumentChunk>(batch.Count);
                    for (var j = 0; j < batch.Count; j++)
                    {
                        var c = batch[j];
                        entities.Add(new DocumentChunk(doc.Id, doc.UserId, c.Text, c.TokenCount, embeddings[j]));
                    }

                    await chunkRepo.AddRangeAsync(entities, ct);
                    await unitOfWork.SaveChangesAsync(ct);
                    inserted += entities.Count;
                }

                sw.Stop();
                logger.LogInformation("KnowledgeEmbeddingCompleted DocumentId={DocumentId} InsertedChunks={InsertedChunks} ElapsedMilliseconds={ElapsedMilliseconds}",
                    documentId, inserted, sw.ElapsedMilliseconds);
            }
        };
    }
}

