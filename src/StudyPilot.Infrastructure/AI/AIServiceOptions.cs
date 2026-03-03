namespace StudyPilot.Infrastructure.AI;

public sealed class AIServiceOptions
{
    public const string SectionName = "AIService";
    public string BaseUrl { get; set; } = "http://study-pilot-ai:8000";
    /// <summary>HTTP timeout for AI service calls.</summary>
    public int TimeoutSeconds { get; set; } = 300;
    /// <summary>LLM call timeout (seconds). Default 30. Used for extract-concepts and generate-quiz.</summary>
    public int LlmTimeoutSeconds { get; set; } = 30;
    public int MaxTextLength { get; set; } = 50_000;
    /// <summary>Max concurrent outbound AI requests (backpressure). Default 4.</summary>
    public int MaxConcurrentRequests { get; set; } = 4;
    /// <summary>Base delay (ms) between question-generation retries (exponential backoff). Lower in Development for faster retries.</summary>
    public int QuestionGenerationRetryBaseDelayMs { get; set; } = 500;
}
