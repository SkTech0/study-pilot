namespace StudyPilot.API.Contracts.Requests;

public sealed record SendChatMessageRequest(Guid SessionId, string Content);
