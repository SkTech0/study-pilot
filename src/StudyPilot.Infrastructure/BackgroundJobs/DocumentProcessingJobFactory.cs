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

    public Func<CancellationToken, Task> CreateProcessDocumentJob(Guid documentId, string? correlationId = null)
    {
        return async (ct) =>
        {
            using var scope = _services.CreateScope();
            var documentRepo = scope.ServiceProvider.GetRequiredService<IDocumentRepository>();
            var conceptRepo = scope.ServiceProvider.GetRequiredService<IConceptRepository>();
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
            if (document is null) return;

            var sw = Stopwatch.StartNew();
            logger.LogInformation("DocumentProcessingStarted DocumentId={DocumentId} CorrelationId={CorrelationId}", documentId, correlationId);

            try
            {
                var text = await fileReader.ReadAllTextAsync(document.StoragePath, ct);
                if (text.Length > aiOptions.MaxTextLength)
                {
                    throw new InvalidOperationException($"Document text length {text.Length} exceeds maximum {aiOptions.MaxTextLength}.");
                }

                var concepts = await aiClient.ExtractConceptsAsync(documentId, text, ct);
                logger.LogInformation("ConceptExtractionCompleted DocumentId={DocumentId} ConceptCount={Count} ElapsedMilliseconds={Elapsed}",
                    documentId, concepts.Count, sw.ElapsedMilliseconds);

                await conceptRepo.DeleteByDocumentIdAsync(documentId, ct);
                await unitOfWork.SaveChangesAsync(ct);

                foreach (var item in concepts)
                {
                    var concept = new Concept(document.Id, item.Name, item.Description);
                    await conceptRepo.AddAsync(concept, ct);
                }
                await unitOfWork.SaveChangesAsync(ct);

                document.SetProcessingStatus(ProcessingStatus.Completed);
                await documentRepo.UpdateAsync(document);
                await unitOfWork.SaveChangesAsync(ct);

                sw.Stop();
                StudyPilotMetrics.DocumentsProcessedTotal.Add(1);
                logger.LogInformation("DocumentProcessingCompleted DocumentId={DocumentId} ElapsedMilliseconds={ElapsedMilliseconds}",
                    documentId, sw.ElapsedMilliseconds);
            }
            catch (OperationCanceledException)
            {
                sw.Stop();
                logger.LogWarning("DocumentProcessingCancelled DocumentId={DocumentId} ElapsedMilliseconds={ElapsedMilliseconds}", documentId, sw.ElapsedMilliseconds);
                try
                {
                    document.SetProcessingStatus(ProcessingStatus.Failed, "Processing was cancelled.");
                    await documentRepo.UpdateAsync(document);
                    await unitOfWork.SaveChangesAsync(CancellationToken.None);
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
                    await unitOfWork.SaveChangesAsync(CancellationToken.None);
                }
                catch { }
            }
            }
        };
    }
}
