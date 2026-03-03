namespace StudyPilot.Application.Abstractions.Knowledge;

/// <summary>
/// Execution priority for embedding jobs. Lower value = higher priority.
/// CRITICAL: Ready completion / stuck embedding repair.
/// HIGH: New embeddings.
/// NORMAL: Retries.
/// LOW: Stale refresh (only when pipeline mode == Normal).
/// </summary>
public enum PipelinePriority
{
    Critical = 0,
    High = 1,
    Normal = 2,
    Low = 3
}
