using System.ComponentModel.DataAnnotations;

namespace StudyPilot.Infrastructure.Persistence;

/// <summary>
/// Outbox entry for knowledge pipeline events (e.g. "DocumentConceptsExtracted").
/// Used to transactionally schedule downstream embedding work.
/// </summary>
public sealed class KnowledgeOutboxEntry
{
    public Guid Id { get; set; }

    /// <summary>Aggregate root identifier (DocumentId).</summary>
    public Guid AggregateId { get; set; }

    /// <summary>Event type discriminator (e.g. "DocumentConceptsExtracted").</summary>
    [MaxLength(200)]
    public string EventType { get; set; } = string.Empty;

    /// <summary>Event payload as JSON (e.g. document id, user id, correlation id).</summary>
    public string Payload { get; set; } = string.Empty;

    /// <summary>Outbox processing status: Pending | Processing | Completed | Failed.</summary>
    [MaxLength(50)]
    public string Status { get; set; } = "Pending";

    public int RetryCount { get; set; }

    public DateTime? NextAttemptUtc { get; set; }

    public DateTime CreatedUtc { get; set; }
}

