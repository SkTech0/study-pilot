using StudyPilot.Application.Abstractions.Knowledge;
using StudyPilot.Application.Knowledge.Models;
using StudyPilot.Infrastructure.AI;

namespace StudyPilot.Infrastructure.Knowledge;

public sealed class ChatService : IChatService
{
    private readonly IStudyPilotKnowledgeAIClient _client;

    public ChatService(IStudyPilotKnowledgeAIClient client) => _client = client;

    public async Task<ChatAnswer> GenerateAnswerAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        var dto = new ChatRequestDto
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
                .ToList()
        };

        var response = await _client.ChatAsync(dto, cancellationToken);
        var cited = new List<Guid>();
        foreach (var id in response.CitedChunkIds ?? new List<string>())
        {
            if (Guid.TryParse(id, out var guid))
                cited.Add(guid);
        }
        return new ChatAnswer(response.Answer ?? "", cited, response.Model);
    }
}

