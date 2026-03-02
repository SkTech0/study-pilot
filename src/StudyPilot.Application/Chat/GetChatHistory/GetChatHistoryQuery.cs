using MediatR;
using StudyPilot.Application.Common.Models;

namespace StudyPilot.Application.Chat.GetChatHistory;

public sealed record GetChatHistoryQuery(Guid UserId, Guid SessionId, int PageNumber = 1, int PageSize = 50) : IRequest<Result<GetChatHistoryResult>>;

