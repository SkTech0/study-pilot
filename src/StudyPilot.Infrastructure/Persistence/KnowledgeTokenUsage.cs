namespace StudyPilot.Infrastructure.Persistence;

/// <summary>
/// Token usage event for rolling 24h budget. Coordinator aggregates over last 24h.
/// </summary>
public sealed class KnowledgeTokenUsage
{
    public long Id { get; set; }
    public DateTime TimestampUtc { get; set; }
    public long EstimatedTokens { get; set; }
}
