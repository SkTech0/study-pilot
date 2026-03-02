using MediatR;
using StudyPilot.Application.Common.Models;

namespace StudyPilot.Application.Chat.SendChatMessage;

public sealed record SendChatMessageCommand(Guid UserId, Guid SessionId, string Content) : IRequest<Result<SendChatMessageResult>>;

