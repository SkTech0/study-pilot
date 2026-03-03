namespace StudyPilot.Domain.Enums;

public enum KnowledgeStatus
{
    None = 0,
    PendingEmbedding = 1,
    Embedding = 2,
    Ready = 3,
    Failed = 4,
    Stale = 5
}

