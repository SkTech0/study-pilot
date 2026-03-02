using System.Text.Json.Serialization;

namespace StudyPilot.API.Contracts.Responses;

public sealed record SubmitQuizResponse(
    [property: JsonPropertyName("correctCount")] int CorrectCount,
    [property: JsonPropertyName("totalCount")] int TotalCount);
