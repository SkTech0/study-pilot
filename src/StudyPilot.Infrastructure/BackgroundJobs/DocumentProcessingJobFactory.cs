using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog.Context;
using StudyPilot.Application.Abstractions.BackgroundJobs;
using StudyPilot.Application.Abstractions.Knowledge;
using StudyPilot.Application.Abstractions.AI;
using StudyPilot.Application.Abstractions.Observability;
using StudyPilot.Application.Abstractions.Persistence;
using StudyPilot.Domain.Entities;
using StudyPilot.Domain.Enums;
using StudyPilot.Domain.Exceptions;
using StudyPilot.Infrastructure.AI;
using StudyPilot.Infrastructure.Persistence;
using StudyPilot.Infrastructure.Persistence.Repositories;
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
            var outboxRepo = scope.ServiceProvider.GetRequiredService<IKnowledgeOutboxRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var aiClient = scope.ServiceProvider.GetRequiredService<IStudyPilotAIClient>();
            var fileReader = scope.ServiceProvider.GetRequiredService<IFileContentReader>();
            var stateMachine = scope.ServiceProvider.GetRequiredService<IKnowledgeStateMachine>();
            var limiter = scope.ServiceProvider.GetRequiredService<IAIExecutionLimiter>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<DocumentProcessingJobFactory>>();
            var correlationAccessor = scope.ServiceProvider.GetService<ICorrelationIdAccessor>();
            var aiOptions = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<AIServiceOptions>>().Value;

            if (!string.IsNullOrEmpty(correlationId))
                correlationAccessor?.Set(correlationId);

            using (LogContext.PushProperty("CorrelationId", correlationId ?? ""))
            {
                var document = await documentRepo.TryClaimForProcessingAsync(documentId, ct).ConfigureAwait(false);
                if (document is null)
                {
                    logger.LogWarning("DocumentNotPendingSkippingJob DocumentId={DocumentId} CorrelationId={CorrelationId}", documentId, correlationId);
                    return;
                }

                var sw = Stopwatch.StartNew();
                logger.LogInformation("StepComplete DocumentId={DocumentId} JobId={JobId} StepName=extraction_start CorrelationId={CorrelationId}", documentId, jobId, correlationId);

                try
                {
                    await stateMachine.WithAIRunningAsync(document, async () =>
                    {
                        string text;
                        var readSw = Stopwatch.StartNew();
                        text = await fileReader.ReadAllTextAsync(document.StoragePath, ct).ConfigureAwait(false);
                        readSw.Stop();
                        StudyPilotMetrics.KnowledgePipelineStageDuration.Record(readSw.Elapsed.TotalMilliseconds, new KeyValuePair<string, object?>("stage", "file_read"));
                        logger.LogInformation("StepComplete DocumentId={DocumentId} StepName=extraction_end DurationMs={DurationMs} CorrelationId={CorrelationId}", documentId, readSw.ElapsedMilliseconds, correlationId);

                        var allConcepts = new List<Concept>();
                        var aiStatus = AIEnrichmentStatus.None;
                        var windowSize = Math.Max(1, aiOptions.MaxTextLength);
                        for (var offset = 0; offset < text.Length; offset += windowSize)
                        {
                            var length = Math.Min(windowSize, text.Length - offset);
                            var slice = text.AsSpan(offset, length).ToString();
                            try
                            {
                                await limiter.WaitForCapacityAsync(ct).ConfigureAwait(false);
                                try
                                {
                                    var windowConcepts = await aiClient.ExtractConceptsAsync(documentId, slice, ct).ConfigureAwait(false);
                                    aiStatus = AIEnrichmentStatus.Completed;
                                    foreach (var item in windowConcepts)
                                        allConcepts.Add(new Concept(document.Id, item.Name, item.Description));
                                }
                                finally
                                {
                                    limiter.Release();
                                }
                            }
                            catch (OperationCanceledException)
                            {
                                throw;
                            }
                            catch (Exception ex)
                            {
                                logger.LogWarning(ex, "ConceptExtractionWindowFailed DocumentId={DocumentId} Offset={Offset} CorrelationId={CorrelationId}", documentId, offset, correlationId);
                                aiStatus = AIEnrichmentStatus.Failed;
                            }
                        }

                        await conceptRepo.DeleteByDocumentIdAsync(documentId, ct).ConfigureAwait(false);
                        if (allConcepts.Count > 0)
                            await conceptRepo.AddRangeAsync(allConcepts, ct).ConfigureAwait(false);

                        stateMachine.TransitionToIngestionComplete(document, aiStatus, null);
                        await documentRepo.UpdateAsync(document, ct).ConfigureAwait(false);

                        var outboxEntry = new KnowledgeOutboxEntry
                        {
                            Id = Guid.NewGuid(),
                            AggregateId = document.Id,
                            EventType = "DocumentConceptsExtracted",
                            Payload = System.Text.Json.JsonSerializer.Serialize(new { DocumentId = document.Id, CorrelationId = correlationId }),
                            Status = "Pending",
                            RetryCount = 0,
                            NextAttemptUtc = null,
                            CreatedUtc = DateTime.UtcNow
                        };
                        await outboxRepo.AddAsync(outboxEntry, ct).ConfigureAwait(false);

                        await unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);

                        sw.Stop();
                        StudyPilotMetrics.DocumentsProcessedTotal.Add(1);
                        StudyPilotMetrics.KnowledgePipelineStageDuration.Record(sw.Elapsed.TotalMilliseconds, new KeyValuePair<string, object?>("stage", "ingestion_complete"));
                        logger.LogInformation("StepComplete DocumentId={DocumentId} StepName=total_pipeline_duration_ms DurationMs={DurationMs} CorrelationId={CorrelationId}", documentId, sw.ElapsedMilliseconds, correlationId);
                    });
                }
                catch (OperationCanceledException)
                {
                    sw.Stop();
                    logger.LogWarning("DocumentProcessingCancelled DocumentId={DocumentId} ElapsedMilliseconds={ElapsedMilliseconds} CorrelationId={CorrelationId}", documentId, sw.ElapsedMilliseconds, correlationId);
                    try
                    {
                        document = await documentRepo.GetByIdAsync(documentId, ct).ConfigureAwait(false);
                        if (document != null)
                        {
                            stateMachine.TransitionToFailed(document, "Processing was cancelled.");
                            await documentRepo.UpdateAsync(document, ct).ConfigureAwait(false);
                            await unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);
                        }
                    }
                    catch { }
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    var isFileReadFailure = ex is not InvalidDocumentStateTransitionException;
                    if (isFileReadFailure)
                        logger.LogError(ex, "DocumentProcessingFailed DocumentId={DocumentId} StepName=file_read CorrelationId={CorrelationId}", documentId, correlationId);
                    else
                        logger.LogError(ex, "DocumentProcessingPersistenceFailed DocumentId={DocumentId} CorrelationId={CorrelationId}", documentId, correlationId);
                    try
                    {
                        document = await documentRepo.GetByIdAsync(documentId, ct).ConfigureAwait(false);
                        if (document != null)
                        {
                            stateMachine.TransitionToFailed(document, SanitizeFailureReason(ex.Message, MaxFailureReasonLength));
                            await documentRepo.UpdateAsync(document, ct).ConfigureAwait(false);
                            await unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);
                        }
                    }
                    catch { }
                }
            }
        };
    }
}
