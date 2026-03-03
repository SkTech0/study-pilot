using StudyPilot.Domain.Entities;
using StudyPilot.Domain.Enums;

namespace StudyPilot.Application.Abstractions.Knowledge;

/// <summary>
/// Central entry point for document lifecycle transitions. All status changes must go through this interface.
/// Validates transitions via DocumentKnowledgePolicy; no direct status mutation allowed elsewhere.
/// </summary>
public interface IKnowledgeStateMachine
{
    /// <summary>Mark document as claimed for processing (Pending -> Processing).</summary>
    void TransitionToProcessing(Document document);

    /// <summary>Mark AI enrichment as started (required before TransitionToIngestionComplete with Completed/Failed).</summary>
    void TransitionToAIRunning(Document document);

    /// <summary>Runs extraction work with AI enrichment set to Running first. Use this for any job that performs AI extraction so the state machine stays valid.</summary>
    Task WithAIRunningAsync(Document document, Func<Task> runExtraction);

    /// <summary>Ingestion complete: Completed, PendingEmbedding, optional AI status.</summary>
    void TransitionToIngestionComplete(Document document, AIEnrichmentStatus? aiStatus, string? failureReason = null);

    /// <summary>Knowledge pipeline: document is being embedded (PendingEmbedding -> Embedding).</summary>
    void TransitionToEmbedding(Document document);

    /// <summary>Knowledge ready for RAG (Embedding -> Ready).</summary>
    void TransitionToReady(Document document);

    /// <summary>Processing or knowledge failed; optional failure reason.</summary>
    void TransitionToFailed(Document document, string? failureReason = null);

    /// <summary>Mark knowledge stale (e.g. embedding model or chunk strategy changed).</summary>
    void TransitionToStale(Document document);

    /// <summary>Retry embedding (Failed or Stale -> PendingEmbedding).</summary>
    void TransitionToPendingEmbedding(Document document);

    /// <summary>Reset processing for retry (Failed -> Pending).</summary>
    void TransitionToPending(Document document);
}
