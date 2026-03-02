namespace StudyPilot.Infrastructure.Services;

public sealed class UsageGuardOptions
{
    public const string SectionName = "UsageGuard";
    public int MaxDocumentsPerUserPerDay { get; set; } = 20;
    public int MaxQuizGenerationPerHour { get; set; } = 10;
}
