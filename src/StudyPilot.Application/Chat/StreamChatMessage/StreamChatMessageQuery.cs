using MediatR;
using StudyPilot.Application.Common.Models;

namespace StudyPilot.Application.Chat.StreamChatMessage;

public sealed record StreamChatMessageQuery(Guid UserId, Guid SessionId, string Message)
    : IRequest<Result<StreamChatMessageResult>>;
