using Microsoft.Extensions.Logging;
using StudyPilot.Application.Abstractions.Knowledge;
using StudyPilot.Domain.Entities;
using StudyPilot.Domain.Enums;

namespace StudyPilot.Infrastructure.Knowledge;

public sealed class KnowledgeStateMachine : IKnowledgeStateMachine
{
    private readonly ILogger<KnowledgeStateMachine> _logger;

    public KnowledgeStateMachine(ILogger<KnowledgeStateMachine> logger) => _logger = logger;

    public void TransitionToProcessing(Document document)
    {
        document.TransitionTo(ProcessingStatus.Processing, null, null, null);
    }

    public void TransitionToAIRunning(Document document)
    {
        document.TransitionTo(null, null, AIEnrichmentStatus.Running, null);
        _logger.LogInformation("TransitionToAIRunning DocumentId={DocumentId} (AI enrichment started)", document.Id);
    }

    public async Task WithAIRunningAsync(Document document, Func<Task> runExtraction)
    {
        TransitionToAIRunning(document);
        await runExtraction().ConfigureAwait(false);
    }

    public void TransitionToIngestionComplete(Document document, AIEnrichmentStatus? aiStatus, string? failureReason = null)
    {
        document.TransitionTo(ProcessingStatus.Completed, KnowledgeStatus.PendingEmbedding, aiStatus, failureReason);
    }

    public void TransitionToEmbedding(Document document)
    {
        document.TransitionTo(null, KnowledgeStatus.Embedding, null, null);
    }

    public void TransitionToReady(Document document)
    {
        document.TransitionTo(null, KnowledgeStatus.Ready, null, null);
    }

    public void TransitionToFailed(Document document, string? failureReason = null)
    {
        document.TransitionTo(ProcessingStatus.Failed, KnowledgeStatus.Failed, AIEnrichmentStatus.Failed, failureReason);
    }

    public void TransitionToStale(Document document)
    {
        document.TransitionTo(null, KnowledgeStatus.Stale, null, null);
    }

    public void TransitionToPendingEmbedding(Document document)
    {
        document.TransitionTo(null, KnowledgeStatus.PendingEmbedding, null, null);
    }

    public void TransitionToPending(Document document)
    {
        document.TransitionTo(ProcessingStatus.Pending, null, null, null);
    }
}
