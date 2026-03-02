using System.Text.Json.Serialization;

namespace StudyPilot.Infrastructure.AI;

public sealed class GenerateQuizResultDto
{
    [JsonPropertyName("questions")]
    public List<QuizQuestionDto> Questions { get; set; } = [];
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
