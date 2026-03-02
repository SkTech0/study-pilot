namespace StudyPilot.Infrastructure.BackgroundJobs;

public interface IBackgroundQueueMetrics
{
    int QueuedCount { get; }
    long ProcessedCount { get; }
    void RecordProcessed();
}
