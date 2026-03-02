namespace StudyPilot.Infrastructure.BackgroundJobs;

public sealed class BackgroundJobOptions
{
    public const string SectionName = "BackgroundJobs";
    /// <summary>Unique worker instance id (e.g. machine name + process id). If empty, a per-process id is used.</summary>
    public string WorkerId { get; set; } = "";
    /// <summary>Seconds between polling for next job. Default 5.</summary>
    public int PollIntervalSeconds { get; set; } = 5;
    /// <summary>Minutes after which a job in Processing is considered stuck and can be re-claimed. Default 15.</summary>
    public int ProcessingTimeoutMinutes { get; set; } = 15;
    /// <summary>Maximum retries per job before marking as Failed. Default 3.</summary>
    public int MaxRetries { get; set; } = 3;
}
