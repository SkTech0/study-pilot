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
using StudyPilot.Application.Auth.Login;
using StudyPilot.Application.Auth.Logout;
using StudyPilot.Application.Auth.Refresh;
using StudyPilot.Application.Auth.Register;

namespace StudyPilot.API.Controllers;

[ApiController]
[Route("auth")]
[AllowAnonymous]
[EnableRateLimiting("auth-policy")]
public sealed class AuthController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IMapper _mapper;
    private readonly ICorrelationIdAccessor? _correlationIdAccessor;

    public AuthController(IMediator mediator, IMapper mapper, ICorrelationIdAccessor? correlationIdAccessor = null)
    {
        _mediator = mediator;
        _mapper = mapper;
        _correlationIdAccessor = correlationIdAccessor;
    }

    [HttpPost("register")]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        var command = _mapper.Map<RegisterCommand>(request);
        var result = await _mediator.Send(command, cancellationToken);
        return result.ToActionResult(_correlationIdAccessor?.Get(), v => _mapper.Map<AuthResponse>(v));
    }

    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var command = _mapper.Map<LoginCommand>(request);
        var result = await _mediator.Send(command, cancellationToken);
        return result.ToActionResult(_correlationIdAccessor?.Get(), v => _mapper.Map<AuthResponse>(v));
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Refresh([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var command = _mapper.Map<RefreshTokenCommand>(request);
        var result = await _mediator.Send(command, cancellationToken);
        return result.ToActionResult(_correlationIdAccessor?.Get(), v => _mapper.Map<AuthResponse>(v));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var command = _mapper.Map<LogoutCommand>(request);
        await _mediator.Send(command, cancellationToken);
        return Ok();
    }
}
