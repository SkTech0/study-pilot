using System.Text.Json.Serialization;

namespace StudyPilot.Infrastructure.AI;

public sealed class GenerateQuizResultDto
{
    [JsonPropertyName("questions")]
    public List<QuizQuestionDto> Questions { get; set; } = [];

    [JsonPropertyName("promptVersion")]
    public string? PromptVersion { get; set; }

    [JsonPropertyName("modelName")]
    public string? ModelName { get; set; }

    [JsonPropertyName("temperature")]
    public double? Temperature { get; set; }

    [JsonPropertyName("tokenUsage")]
    public int? TokenUsage { get; set; }
}

public sealed class QuizQuestionDto
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = "";

    [JsonPropertyName("options")]
    public List<string> Options { get; set; } = [];

    [JsonPropertyName("correctAnswer")]
    public string CorrectAnswer { get; set; } = "";
}
