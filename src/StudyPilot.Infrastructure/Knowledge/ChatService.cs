using StudyPilot.Application.Abstractions.Knowledge;
using StudyPilot.Application.Knowledge.Models;
using StudyPilot.Infrastructure.AI;
using StudyPilot.Infrastructure.Services;

namespace StudyPilot.Infrastructure.Knowledge;

public sealed class ChatService : IChatService
{
    private readonly IStudyPilotKnowledgeAIClient _client;

    public ChatService(IStudyPilotKnowledgeAIClient client) => _client = client;

    public async Task<ChatAnswer> GenerateAnswerAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        var dto = ToDto(request);
        if (request.ExplanationStyle.HasValue)
            StudyPilotMetrics.ExplanationStyleUsed.Add(1);
        var response = await _client.ChatAsync(dto, cancellationToken);
        var cited = ParseCitedIds(response.CitedChunkIds);
        return new ChatAnswer(response.Answer ?? "", cited, response.Model);
    }

    public async Task<StreamChatResult> StreamChatAsync(
        ChatRequest request,
        Func<string, Task> onToken,
        CancellationToken cancellationToken = default)
    {
        var dto = ToDto(request);
        if (request.ExplanationStyle.HasValue)
            StudyPilotMetrics.ExplanationStyleUsed.Add(1);
        var result = await _client.StreamChatAsync(dto, onToken, cancellationToken);
        var cited = ParseCitedIds(result.CitedChunkIds ?? new List<string>());
        return new StreamChatResult(cited, result.Model);
    }

    private static ChatRequestDto ToDto(ChatRequest request) => new()
    {
        SessionId = request.SessionId.ToString(),
        UserId = request.UserId.ToString(),
        DocumentId = request.DocumentId?.ToString(),
        System = request.SystemInstruction,
        Question = request.Question,
        Context = request.ContextChunks
            .Select(c => new ChatContextChunkDto
            {
                ChunkId = c.ChunkId.ToString(),
                DocumentId = c.DocumentId.ToString(),
                Text = c.Text
            })
            .ToList(),
        ExplanationStyle = request.ExplanationStyle?.ToString()
    };

    private static IReadOnlyList<Guid> ParseCitedIds(IEnumerable<string> ids)
    {
        var list = new List<Guid>();
        foreach (var id in ids ?? Array.Empty<string>())
            if (Guid.TryParse(id, out var guid))
                list.Add(guid);
        return list;
    }
}

