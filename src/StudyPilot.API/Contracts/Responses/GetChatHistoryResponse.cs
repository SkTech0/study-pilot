namespace StudyPilot.API.Contracts.Responses;

public sealed record GetChatHistoryResponse(
    Guid SessionId,
    int TotalCount,
    int PageNumber,
    int PageSize,
    IReadOnlyList<ChatMessageItemResponse> Messages);
