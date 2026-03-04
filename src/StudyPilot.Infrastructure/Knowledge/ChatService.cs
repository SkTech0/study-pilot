using StudyPilot.Application.Abstractions.Knowledge;
using StudyPilot.Application.Chat;
using StudyPilot.Application.Knowledge.Models;
using StudyPilot.Infrastructure.AI;
using StudyPilot.Infrastructure.Services;

namespace StudyPilot.Infrastructure.Knowledge;

public sealed class ChatService : IChatService
{
    private readonly IStudyPilotKnowledgeAIClient _client;

    public ChatService(IStudyPilotKnowledgeAIClient client) => _client = client;

    private const string EmptyAnswerFallback = "I'm temporarily unable to get a response from the AI. Please try again shortly.";

    public async Task<ChatAnswer> GenerateAnswerAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        var dto = ToDto(request);
        if (request.ExplanationStyle.HasValue)
            StudyPilotMetrics.ExplanationStyleUsed.Add(1);
        var response = await _client.ChatAsync(dto, cancellationToken);
        var answer = (response.Answer ?? "").Trim();
        var fallbackUsed = false;
        if (string.IsNullOrWhiteSpace(answer))
        {
            answer = EmptyAnswerFallback;
            fallbackUsed = true;
        }
        var cited = ParseCitedIds(response.CitedChunkIds);
        return new ChatAnswer(answer, cited, response.Model, fallbackUsed ? ChatStatus.Fallback : ChatStatus.Ok, fallbackUsed);
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
        return new StreamChatResult(cited, result.Model, result.FallbackUsed ? ChatStatus.Fallback : ChatStatus.Ok, result.FallbackUsed);
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

