namespace StudyPilot.Application.Abstractions.BackgroundJobs;

public interface IBackgroundJobQueue
{
    void Enqueue(Func<CancellationToken, Task> job);
}
