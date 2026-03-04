using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog.Context;
using StudyPilot.Application.Abstractions.BackgroundJobs;
using StudyPilot.Application.Abstractions.Knowledge;
using StudyPilot.Application.Abstractions.AI;
using StudyPilot.Application.Abstractions.Observability;
using StudyPilot.Application.Abstractions.Optimization;
using StudyPilot.Application.Abstractions.Persistence;
using StudyPilot.Domain.Entities;
using StudyPilot.Infrastructure.AI;
using StudyPilot.Infrastructure.Knowledge.Chunking;
using StudyPilot.Infrastructure.Storage;
using StudyPilot.Infrastructure.Services;

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
            var stateMachine = scope.ServiceProvider.GetRequiredService<IKnowledgeStateMachine>();
            var limiter = scope.ServiceProvider.GetRequiredService<IAIExecutionLimiter>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<KnowledgeEmbeddingJobFactory>>();
            var correlationAccessor = scope.ServiceProvider.GetService<ICorrelationIdAccessor>();
            var aiOptions = scope.ServiceProvider.GetRequiredService<IOptions<AIServiceOptions>>().Value;
            var optimizationConfig = scope.ServiceProvider.GetRequiredService<IOptimizationConfigProvider>();
            var chunkSizeTokens = Math.Clamp(await optimizationConfig.GetChunkSizeTokensAsync(ct).ConfigureAwait(false), 200, 2000);
            var batchSize = Math.Clamp(await optimizationConfig.GetEmbeddingBatchSizeAsync(ct).ConfigureAwait(false), 4, 128);

            if (!string.IsNullOrEmpty(correlationId))
                correlationAccessor?.Set(correlationId);

            using (LogContext.PushProperty("CorrelationId", correlationId ?? ""))
            {
                var doc = await documentRepo.GetByIdAsync(documentId, ct).ConfigureAwait(false);
                if (doc is null) return;

                stateMachine.TransitionToEmbedding(doc);
                await documentRepo.UpdateAsync(doc, ct).ConfigureAwait(false);
                await unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);

                var sw = Stopwatch.StartNew();
                logger.LogInformation("KnowledgeEmbeddingStarted DocumentId={DocumentId} UserId={UserId} CorrelationId={CorrelationId}", documentId, doc.UserId, correlationId);

                string text;
                try
                {
                    text = await fileReader.ReadAllTextAsync(doc.StoragePath, ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "KnowledgeEmbeddingFileReadFailed DocumentId={DocumentId} CorrelationId={CorrelationId}", documentId, correlationId);
                    doc = await documentRepo.GetByIdAsync(documentId, ct).ConfigureAwait(false);
                    if (doc != null)
                    {
                        stateMachine.TransitionToPendingEmbedding(doc);
                        await documentRepo.UpdateAsync(doc, ct).ConfigureAwait(false);
                        await unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);
                    }
                    return;
                }

                var embeddingVersion = Math.Max(1, aiOptions.EmbeddingVersion);
                var chunkingVersion = Math.Max(1, aiOptions.ChunkingVersion);
                var embeddingModel = aiOptions.EmbeddingModelId ?? "default";
                var embeddedAt = DateTime.UtcNow;

                var windowSize = Math.Max(1, aiOptions.MaxTextLength);
                var allChunks = new List<(string Text, int TokenCount)>();
                for (var offset = 0; offset < text.Length; offset += windowSize)
                {
                    var length = Math.Min(windowSize, text.Length - offset);
                    var window = text.AsSpan(offset, length).ToString();
                    var windowChunks = TextChunker.Chunk(window, targetTokens: chunkSizeTokens, overlapTokens: 150);
                    foreach (var c in windowChunks)
                        allChunks.Add((c.Text, c.TokenCount));
                }

                logger.LogInformation("KnowledgeChunkingCompleted DocumentId={DocumentId} ChunkCount={ChunkCount} ElapsedMs={ElapsedMs} CorrelationId={CorrelationId}",
                    documentId, allChunks.Count, sw.ElapsedMilliseconds, correlationId);

                await chunkRepo.DeleteByDocumentIdAsync(documentId, ct).ConfigureAwait(false);
                await unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);

                var inserted = 0;
                try
                {
                    for (var i = 0; i < allChunks.Count; i += batchSize)
                    {
                        var batch = allChunks.Skip(i).Take(batchSize).ToList();
                        var texts = batch.Select(b => b.Text).ToList();
                        await limiter.WaitForCapacityAsync(ct).ConfigureAwait(false);
                        float[][] embeddings;
                        try
                        {
                            embeddings = (await embeddingService.EmbedBatchAsync(texts, ct).ConfigureAwait(false)).ToArray();
                        }
                        finally
                        {
                            limiter.Release();
                        }

                        var entities = new List<DocumentChunk>(batch.Count);
                        for (var j = 0; j < batch.Count; j++)
                        {
                            var (chunkText, tokenCount) = batch[j];
                            entities.Add(new DocumentChunk(
                                doc.Id,
                                doc.UserId,
                                chunkText,
                                tokenCount,
                                embeddings[j],
                                embeddingVersion,
                                embeddingModel,
                                chunkingVersion,
                                embeddedAt));
                        }
                        await chunkRepo.AddRangeAsync(entities, ct).ConfigureAwait(false);
                        await unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);
                        inserted += entities.Count;
                    }

                    doc = await documentRepo.GetByIdAsync(documentId, ct).ConfigureAwait(false);
                    if (doc != null)
                    {
                        stateMachine.TransitionToReady(doc);
                        await documentRepo.UpdateAsync(doc, ct).ConfigureAwait(false);
                        await unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);
                    }

                    sw.Stop();
                    StudyPilotMetrics.KnowledgeEmbeddingLatency.Record(sw.Elapsed.TotalMilliseconds);
                    var estimatedTokens = inserted * 800;
                    var tokenUsageRepo = scope.ServiceProvider.GetService<StudyPilot.Infrastructure.Persistence.Repositories.IKnowledgeTokenUsageRepository>();
                    if (tokenUsageRepo != null)
                    {
                        try
                        {
                            await tokenUsageRepo.AddAsync(new StudyPilot.Infrastructure.Persistence.KnowledgeTokenUsage
                            {
                                TimestampUtc = DateTime.UtcNow,
                                EstimatedTokens = estimatedTokens
                            }, ct).ConfigureAwait(false);
                        }
                        catch { /* best effort */ }
                    }
                    var coordinator = scope.ServiceProvider.GetService<IKnowledgePipelineCoordinator>();
                    coordinator?.RecordEstimatedTokenUsage(estimatedTokens);
                    logger.LogInformation("KnowledgeEmbeddingCompleted DocumentId={DocumentId} InsertedChunks={InsertedChunks} ElapsedMs={ElapsedMs} CorrelationId={CorrelationId}",
                        documentId, inserted, sw.ElapsedMilliseconds, correlationId);
                }
                catch (OperationCanceledException)
                {
                    doc = await documentRepo.GetByIdAsync(documentId, ct).ConfigureAwait(false);
                    if (doc != null)
                    {
                        stateMachine.TransitionToPendingEmbedding(doc);
                        await documentRepo.UpdateAsync(doc, ct).ConfigureAwait(false);
                        await unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);
                    }
                    logger.LogWarning("KnowledgeEmbeddingCancelled DocumentId={DocumentId} CorrelationId={CorrelationId}", documentId, correlationId);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "KnowledgeEmbeddingFailed DocumentId={DocumentId} CorrelationId={CorrelationId}", documentId, correlationId);
                    doc = await documentRepo.GetByIdAsync(documentId, ct).ConfigureAwait(false);
                    if (doc != null)
                    {
                        stateMachine.TransitionToPendingEmbedding(doc);
                        await documentRepo.UpdateAsync(doc, ct).ConfigureAwait(false);
                        await unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);
                    }
                }
            }
        };
    }
}
