using StudyPilot.Application.Knowledge.Models;

namespace StudyPilot.Application.Abstractions.Knowledge;

public interface IChatService
{
    Task<ChatAnswer> GenerateAnswerAsync(ChatRequest request, CancellationToken cancellationToken = default);
}

