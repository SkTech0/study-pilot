using StudyPilot.Domain.Common;
using StudyPilot.Domain.Enums;
using StudyPilot.Domain.Exceptions;
using StudyPilot.Domain.Knowledge;

namespace StudyPilot.Domain.Entities;

public class Document : BaseEntity
{
    public Guid UserId { get; private set; }
    public string FileName { get; private set; }
    public string StoragePath { get; private set; }

    /// <summary>End-to-end ingestion/parsing status (upload / text extraction).</summary>
    public ProcessingStatus ProcessingStatus { get; private set; }

    /// <summary>RAG / knowledge readiness lifecycle for this document.</summary>
    public KnowledgeStatus KnowledgeStatus { get; private set; } = KnowledgeStatus.None;

    /// <summary>Optional high-level status for AI enrichment (concept extraction); null when not evaluated.</summary>
    public AIEnrichmentStatus? AIEnrichmentStatus { get; private set; }

    /// <summary>When status is Failed, optional reason for the failure (e.g. from exception message).</summary>
    public string? FailureReason { get; private set; }

    private readonly List<Concept> _concepts = new();
    public IReadOnlyCollection<Concept> Concepts => _concepts.AsReadOnly();

    public Document(Guid userId, string fileName, string storagePath) : base()
    {
        UserId = userId;
        FileName = string.IsNullOrWhiteSpace(fileName)
            ? throw new ArgumentException("File name cannot be empty.", nameof(fileName))
            : fileName;
        StoragePath = string.IsNullOrWhiteSpace(storagePath)
            ? throw new ArgumentException("Storage path cannot be empty.", nameof(storagePath))
            : storagePath;
        ProcessingStatus = ProcessingStatus.Pending;
        KnowledgeStatus = KnowledgeStatus.None;
        AIEnrichmentStatus = null;
    }

    public Document(
        Guid id,
        Guid userId,
        string fileName,
        string storagePath,
        ProcessingStatus processingStatus,
        DateTime createdAtUtc,
        DateTime updatedAtUtc,
        string? failureReason = null,
        KnowledgeStatus knowledgeStatus = KnowledgeStatus.None,
        AIEnrichmentStatus? aiEnrichmentStatus = null) : base(id, createdAtUtc, updatedAtUtc)
    {
        UserId = userId;
        FileName = fileName;
        StoragePath = storagePath;
        ProcessingStatus = processingStatus;
        FailureReason = failureReason;
        KnowledgeStatus = knowledgeStatus;
        AIEnrichmentStatus = aiEnrichmentStatus;
    }

    public void AddConcept(Concept concept)
    {
        if (concept is null)
            throw new ArgumentNullException(nameof(concept));
        if (concept.DocumentId != Id)
            throw new InvalidOperationException("Concept must belong to this document.");
        _concepts.Add(concept);
        Touch();
    }

    /// <summary>
    /// Single entry point for status changes. Validates via DocumentKnowledgePolicy; throws on invalid transition.
    /// Pass null for any dimension to leave it unchanged.
    /// </summary>
    public void TransitionTo(
        ProcessingStatus? nextProcessing,
        KnowledgeStatus? nextKnowledge,
        AIEnrichmentStatus? nextAi,
        string? failureReason = null)
    {
        if (!DocumentKnowledgePolicy.CanTransition(
            ProcessingStatus, KnowledgeStatus, AIEnrichmentStatus,
            nextProcessing, nextKnowledge, nextAi))
        {
            throw new InvalidDocumentStateTransitionException(
                $"Invalid document state transition: Processing {ProcessingStatus}->{nextProcessing}, Knowledge {KnowledgeStatus}->{nextKnowledge}, AI {AIEnrichmentStatus}->{nextAi}.");
        }

        if (nextProcessing.HasValue)
            ProcessingStatus = nextProcessing.Value;
        if (nextKnowledge.HasValue)
            KnowledgeStatus = nextKnowledge.Value;
        if (nextAi.HasValue)
            AIEnrichmentStatus = nextAi.Value;
        if (failureReason != null)
            FailureReason = failureReason;
        Touch();
    }
}
