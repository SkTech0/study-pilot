using System.Text.Json.Serialization;

namespace StudyPilot.Infrastructure.AI;

public sealed class ConceptDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}
