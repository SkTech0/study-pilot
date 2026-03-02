namespace StudyPilot.Infrastructure.Services;

public sealed class UsageGuardOptions
{
    public const string SectionName = "UsageGuard";

    /// <summary>Max document uploads per user per calendar day. Override in config if needed.</summary>
    public int MaxDocumentsPerUserPerDay { get; set; } = 20;

    /// <summary>Max quiz generations per user per rolling hour. Use a higher value for dev/testing (e.g. 30).</summary>
    public int MaxQuizGenerationPerHour { get; set; } = 30;
}
