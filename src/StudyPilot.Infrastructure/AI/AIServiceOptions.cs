namespace StudyPilot.Infrastructure.AI;

public sealed class AIServiceOptions
{
    public const string SectionName = "AIService";
    public string BaseUrl { get; set; } = "http://study-pilot-ai:8000";
    public int TimeoutSeconds { get; set; } = 60;
    public int MaxTextLength { get; set; } = 50_000;
}
