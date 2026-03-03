namespace StudyPilot.Application.Chat.StreamChatMessage;

public sealed record StreamChatMessageResult(
    IAsyncEnumerable<string> Tokens,
    Task WhenComplete);
