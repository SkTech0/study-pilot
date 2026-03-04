using StudyPilot.Domain.Enums;

namespace StudyPilot.Domain.Knowledge;

/// <summary>
/// Central policy for allowed document lifecycle transitions.
/// No status mutation is valid unless it passes this policy.
/// </summary>
public static class DocumentKnowledgePolicy
{
    /// <summary>
    /// Validates a transition from (currentProcessing, currentKnowledge, currentAi)
    /// to (nextProcessing, nextKnowledge, nextAi).
    /// </summary>
    public static bool CanTransition(
        ProcessingStatus currentProcessing,
        KnowledgeStatus currentKnowledge,
        AIEnrichmentStatus? currentAi,
        ProcessingStatus? nextProcessing,
        KnowledgeStatus? nextKnowledge,
        AIEnrichmentStatus? nextAi)
    {
        if (nextProcessing.HasValue && !CanTransitionProcessing(currentProcessing, nextProcessing.Value))
            return false;
        if (nextKnowledge.HasValue && !CanTransitionKnowledge(currentKnowledge, nextKnowledge.Value))
            return false;
        if (nextAi.HasValue && !CanTransitionAi(currentAi, nextAi.Value))
            return false;

        return true;
    }

    public static bool CanTransitionProcessing(ProcessingStatus current, ProcessingStatus next)
    {
        return (current, next) switch
        {
            (ProcessingStatus.Pending, ProcessingStatus.Processing) => true,
            (ProcessingStatus.Processing, ProcessingStatus.Completed) => true,
            (ProcessingStatus.Processing, ProcessingStatus.Failed) => true,
            (ProcessingStatus.Processing, ProcessingStatus.Pending) => true, // Stuck recovery / retry: reset for re-claim
            (ProcessingStatus.Failed, ProcessingStatus.Pending) => true,
            _ => false
        };
    }

    public static bool CanTransitionKnowledge(KnowledgeStatus current, KnowledgeStatus next)
    {
        return (current, next) switch
        {
            (KnowledgeStatus.None, KnowledgeStatus.PendingEmbedding) => true,
            (KnowledgeStatus.PendingEmbedding, KnowledgeStatus.Embedding) => true,
            (KnowledgeStatus.Embedding, KnowledgeStatus.Ready) => true,
            (KnowledgeStatus.Embedding, KnowledgeStatus.Failed) => true,
            (KnowledgeStatus.Embedding, KnowledgeStatus.PendingEmbedding) => true,
            (KnowledgeStatus.Failed, KnowledgeStatus.PendingEmbedding) => true,
            (KnowledgeStatus.Stale, KnowledgeStatus.PendingEmbedding) => true,
            (KnowledgeStatus.Ready, KnowledgeStatus.Stale) => true,
            _ => false
        };
    }

    /// <summary>
    /// AI enrichment transitions. Completed/Failed require current state Running (call TransitionToAIRunning before extraction).
    /// </summary>
    public static bool CanTransitionAi(AIEnrichmentStatus? current, AIEnrichmentStatus next)
    {
        return (current, next) switch
        {
            (null, AIEnrichmentStatus.None) => true,
            (null, AIEnrichmentStatus.Running) => true,
            (null, AIEnrichmentStatus.Failed) => true,
            (AIEnrichmentStatus.None, AIEnrichmentStatus.Running) => true,
            (AIEnrichmentStatus.None, AIEnrichmentStatus.Failed) => true,
            (AIEnrichmentStatus.Running, AIEnrichmentStatus.Completed) => true,
            (AIEnrichmentStatus.Running, AIEnrichmentStatus.Failed) => true,
            (AIEnrichmentStatus.Failed, AIEnrichmentStatus.Running) => true,
            (AIEnrichmentStatus.Failed, AIEnrichmentStatus.None) => true,
            _ => false
        };
    }
}
