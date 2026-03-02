namespace StudyPilot.API.Contracts.Responses;

public sealed record SendChatMessageResponse(Guid AssistantMessageId, string Answer, IReadOnlyList<Guid> CitedChunkIds);
