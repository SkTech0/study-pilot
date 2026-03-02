namespace StudyPilot.Infrastructure.AI;

public sealed class OpenAIServiceOptions
{
    public const string SectionName = "OpenAI";
    public string? ApiKey { get; set; }
    public string BaseUrl { get; set; } = "https://api.openai.com/v1";
}
