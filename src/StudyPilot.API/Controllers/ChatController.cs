using AutoMapper;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using StudyPilot.API.Contracts;
using StudyPilot.API.Contracts.Requests;
using StudyPilot.API.Contracts.Responses;
using StudyPilot.API.Extensions;
using StudyPilot.Application.Abstractions.Observability;
using StudyPilot.Application.Chat.CreateChatSession;
using StudyPilot.Application.Chat.GetChatHistory;
using StudyPilot.Application.Chat.SendChatMessage;

namespace StudyPilot.API.Controllers;

[ApiController]
[Route("chat")]
[Authorize]
[EnableRateLimiting("chat-policy")]
public sealed class ChatController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IMapper _mapper;
    private readonly ICorrelationIdAccessor? _correlationIdAccessor;

    public ChatController(IMediator mediator, IMapper mapper, ICorrelationIdAccessor? correlationIdAccessor = null)
    {
        _mediator = mediator;
        _mapper = mapper;
        _correlationIdAccessor = correlationIdAccessor;
    }

    [HttpPost("sessions")]
    public async Task<ActionResult<ApiResponse<CreateChatSessionResponse>>> CreateSession([FromBody] CreateChatSessionRequest request, CancellationToken cancellationToken)
    {
        if (this.UnauthorizedIfNoUser<CreateChatSessionResponse>(_correlationIdAccessor) is { } unauthorized)
            return unauthorized;
        var userId = User.GetCurrentUserId()!.Value;
        var command = new CreateChatSessionCommand(userId, request.DocumentId);
        var result = await _mediator.Send(command, cancellationToken);
        return result.ToActionResult(_correlationIdAccessor?.Get(), v => _mapper.Map<CreateChatSessionResponse>(v));
    }

    [HttpPost("message")]
    public async Task<ActionResult<ApiResponse<SendChatMessageResponse>>> SendMessage([FromBody] SendChatMessageRequest request, CancellationToken cancellationToken)
    {
        if (this.UnauthorizedIfNoUser<SendChatMessageResponse>(_correlationIdAccessor) is { } unauthorized)
            return unauthorized;
        var userId = User.GetCurrentUserId()!.Value;
        var command = new SendChatMessageCommand(userId, request.SessionId, request.Content?.Trim() ?? "");
        var result = await _mediator.Send(command, cancellationToken);
        return result.ToActionResult(_correlationIdAccessor?.Get(), v => _mapper.Map<SendChatMessageResponse>(v));
    }

    [HttpGet("history/{sessionId:guid}")]
    public async Task<ActionResult<ApiResponse<GetChatHistoryResponse>>> GetHistory([FromRoute] Guid sessionId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
    {
        if (this.UnauthorizedIfNoUser<GetChatHistoryResponse>(_correlationIdAccessor) is { } unauthorized)
            return unauthorized;
        var userId = User.GetCurrentUserId()!.Value;
        var query = new GetChatHistoryQuery(userId, sessionId, pageNumber, pageSize);
        var result = await _mediator.Send(query, cancellationToken);
        return result.ToActionResult(_correlationIdAccessor?.Get(), v => _mapper.Map<GetChatHistoryResponse>(v));
    }
}
