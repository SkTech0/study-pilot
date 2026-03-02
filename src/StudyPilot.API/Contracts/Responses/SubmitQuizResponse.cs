using System.Text.Json.Serialization;

namespace StudyPilot.API.Contracts.Responses;

public sealed record SubmitQuizResponse(
    [property: JsonPropertyName("correctCount")] int CorrectCount,
    [property: JsonPropertyName("totalCount")] int TotalCount,
    [property: JsonPropertyName("questionResults")] IReadOnlyList<QuestionResultResponse>? QuestionResults = null);

public sealed record QuestionResultResponse(
    [property: JsonPropertyName("questionId")] Guid QuestionId,
    [property: JsonPropertyName("isCorrect")] bool IsCorrect,
    [property: JsonPropertyName("correctAnswer")] string CorrectAnswer,
    [property: JsonPropertyName("correctOptionIndex")] int CorrectOptionIndex);
