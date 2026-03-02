using MediatR;
using StudyPilot.Application.Common.Models;

namespace StudyPilot.Application.Chat.CreateChatSession;

public sealed record CreateChatSessionCommand(Guid UserId, Guid? DocumentId) : IRequest<Result<CreateChatSessionResult>>;

