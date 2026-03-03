using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog.Context;
using StudyPilot.Application.Abstractions.BackgroundJobs;
using StudyPilot.Application.Abstractions.Observability;
using StudyPilot.Application.Abstractions.Persistence;
using StudyPilot.Domain.Entities;
using StudyPilot.Domain.Enums;
using StudyPilot.Infrastructure.AI;
using StudyPilot.Infrastructure.Services;
using StudyPilot.Infrastructure.Storage;

namespace StudyPilot.Infrastructure.BackgroundJobs;

public sealed class DocumentProcessingJobFactory : IDocumentProcessingJobFactory
{
    private const int MaxFailureReasonLength = 500;
    private readonly IServiceProvider _services;

    public DocumentProcessingJobFactory(IServiceProvider services) => _services = services;

    private static string SanitizeFailureReason(string message, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(message)) return "An error occurred during processing.";
        var sanitized = message.Trim();
        if (sanitized.Length > maxLength) sanitized = sanitized[..maxLength];
        return sanitized;
    }

    public Func<CancellationToken, Task> CreateProcessDocumentJob(Guid documentId, string? correlationId = null, Guid? jobId = null)
    {
        return async (ct) =>
        {
            await using var scope = _services.CreateAsyncScope();
            var documentRepo = scope.ServiceProvider.GetRequiredService<IDocumentRepository>();
            var conceptRepo = scope.ServiceProvider.GetRequiredService<IConceptRepository>();
            var embeddingQueue = scope.ServiceProvider.GetRequiredService<IKnowledgeEmbeddingJobQueue>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var aiClient = scope.ServiceProvider.GetRequiredService<IStudyPilotAIClient>();
            var fileReader = scope.ServiceProvider.GetRequiredService<IFileContentReader>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<DocumentProcessingJobFactory>>();
            var correlationAccessor = scope.ServiceProvider.GetService<ICorrelationIdAccessor>();
            var aiOptions = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<AIServiceOptions>>().Value;

            if (!string.IsNullOrEmpty(correlationId))
            {
                correlationAccessor?.Set(correlationId);
            }

            using (LogContext.PushProperty("CorrelationId", correlationId ?? ""))
            {

            var document = await documentRepo.TryClaimForProcessingAsync(documentId, ct);
            if (document is null)
            {
                logger.LogWarning("DocumentNotPendingSkippingJob DocumentId={DocumentId} CorrelationId={CorrelationId} (document already Processing/Completed/Failed or missing)",
                    documentId, correlationId);
                return;
            }

            var sw = Stopwatch.StartNew();
            logger.LogInformation("StepComplete DocumentId={DocumentId} JobId={JobId} StepName=extraction_start CorrelationId={CorrelationId}", documentId, jobId, correlationId);

            try
            {
                var readSw = Stopwatch.StartNew();
                var text = await fileReader.ReadAllTextAsync(document.StoragePath, ct);
                if (text.Length > aiOptions.MaxTextLength)
                    throw new InvalidOperationException($"Document text length {text.Length} exceeds maximum {aiOptions.MaxTextLength}.");
                readSw.Stop();
                logger.LogInformation("StepComplete DocumentId={DocumentId} StepName=extraction_end DurationMs={DurationMs} CorrelationId={CorrelationId}", documentId, readSw.ElapsedMilliseconds, correlationId);

                var concepts = await aiClient.ExtractConceptsAsync(documentId, text, ct);

                var persistSw = Stopwatch.StartNew();
                await conceptRepo.DeleteByDocumentIdAsync(documentId, ct);
                var conceptEntities = concepts.Select(item => new Concept(document.Id, item.Name, item.Description)).ToList();
                await conceptRepo.AddRangeAsync(conceptEntities, ct);
                document.SetProcessingStatus(ProcessingStatus.Completed);
                await documentRepo.UpdateAsync(document);
                await unitOfWork.SaveChangesAsync(ct);
                persistSw.Stop();
                logger.LogInformation("StepComplete DocumentId={DocumentId} StepName=persistence_duration_ms DurationMs={DurationMs} CorrelationId={CorrelationId}", documentId, persistSw.ElapsedMilliseconds, correlationId);

                try
                {
                    await embeddingQueue.EnqueueCreateEmbeddingsAsync(documentId, correlationId, ct);
                }
                catch (Exception)
                {
                    logger.LogWarning("StepComplete DocumentId={DocumentId} StepName=embedding_enqueue_failed CorrelationId={CorrelationId}", documentId, correlationId);
                }

                sw.Stop();
                StudyPilotMetrics.DocumentsProcessedTotal.Add(1);
                logger.LogInformation("StepComplete DocumentId={DocumentId} StepName=total_pipeline_duration_ms DurationMs={DurationMs} CorrelationId={CorrelationId}", documentId, sw.ElapsedMilliseconds, correlationId);
            }
            catch (OperationCanceledException)
            {
                sw.Stop();
                logger.LogWarning("DocumentProcessingCancelled DocumentId={DocumentId} ElapsedMilliseconds={ElapsedMilliseconds}", documentId, sw.ElapsedMilliseconds);
                try
                {
                    document.SetProcessingStatus(ProcessingStatus.Failed, "Processing was cancelled.");
                    await documentRepo.UpdateAsync(document);
                    await unitOfWork.SaveChangesAsync(ct);
                }
                catch { }
            }
            catch (Exception ex)
            {
                sw.Stop();
                logger.LogError(ex, "DocumentProcessingFailed DocumentId={DocumentId} ElapsedMilliseconds={ElapsedMilliseconds}",
                    documentId, sw.ElapsedMilliseconds);
                try
                {
                    var reason = SanitizeFailureReason(ex.Message, MaxFailureReasonLength);
                    document.SetProcessingStatus(ProcessingStatus.Failed, reason);
                    await documentRepo.UpdateAsync(document);
                    await unitOfWork.SaveChangesAsync(ct);
                }
                catch { }
            }
            }
        };
    }
}
