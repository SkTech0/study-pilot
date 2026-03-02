namespace StudyPilot.Infrastructure.AI;

public interface IStudyPilotKnowledgeAIClient
{
    Task<EmbeddingsResultDto> CreateEmbeddingsAsync(IReadOnlyList<string> texts, CancellationToken ct = default);
    Task<ChatResultDto> ChatAsync(ChatRequestDto request, CancellationToken ct = default);
}

public sealed class EmbeddingsResultDto
{
    public List<float[]> Embeddings { get; set; } = [];
    public string? Model { get; set; }
}

public sealed class ChatRequestDto
{
    public string SessionId { get; set; } = "";
    public string UserId { get; set; } = "";
    public string? DocumentId { get; set; }
    public string System { get; set; } = "";
    public string Question { get; set; } = "";
    public List<ChatContextChunkDto> Context { get; set; } = [];
}

public sealed class ChatContextChunkDto
{
    public string ChunkId { get; set; } = "";
    public string Text { get; set; } = "";
    public string DocumentId { get; set; } = "";
}

public sealed class ChatResultDto
{
    public string Answer { get; set; } = "";
    public List<string> CitedChunkIds { get; set; } = [];
    public string? Model { get; set; }
}

