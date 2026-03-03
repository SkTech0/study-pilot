using StudyPilot.Application.Knowledge.Models;

namespace StudyPilot.Application.Abstractions.Knowledge;

public interface IChatService
{
    Task<ChatAnswer> GenerateAnswerAsync(ChatRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams tokens via callback, then returns cited chunk ids and model when stream completes.
    /// </summary>
    Task<StreamChatResult> StreamChatAsync(
        ChatRequest request,
        Func<string, Task> onToken,
        CancellationToken cancellationToken = default);
}

