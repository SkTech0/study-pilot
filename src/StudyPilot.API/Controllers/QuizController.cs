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
using StudyPilot.Application.Common.Models;
using StudyPilot.Application.Quiz.StartQuiz;
using StudyPilot.Application.Quiz.SubmitQuiz;

namespace StudyPilot.API.Controllers;

[ApiController]
[Route("quiz")]
[Authorize]
public sealed class QuizController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IMapper _mapper;
    private readonly ICorrelationIdAccessor? _correlationIdAccessor;

    public QuizController(IMediator mediator, IMapper mapper, ICorrelationIdAccessor? correlationIdAccessor = null)
    {
        _mediator = mediator;
        _mapper = mapper;
        _correlationIdAccessor = correlationIdAccessor;
    }

    [HttpPost("start")]
    [EnableRateLimiting("quiz-policy")]
    public async Task<ActionResult<ApiResponse<StartQuizResponse>>> Start([FromBody] StartQuizRequest request, CancellationToken cancellationToken)
    {
        if (this.UnauthorizedIfNoUser<StartQuizResponse>(_correlationIdAccessor) is { } unauthorized)
            return unauthorized;
        var userId = User.GetCurrentUserId()!.Value;
        var command = new StartQuizCommand(request.DocumentId, userId);
        var result = await _mediator.Send(command, cancellationToken);
        return result.ToActionResult(_correlationIdAccessor?.Get(), v => _mapper.Map<StartQuizResponse>(v));
    }

    [HttpPost("submit")]
    public async Task<ActionResult<ApiResponse<SubmitQuizResponse>>> Submit([FromBody] SubmitQuizRequest request, CancellationToken cancellationToken)
    {
        if (this.UnauthorizedIfNoUser<SubmitQuizResponse>(_correlationIdAccessor) is { } unauthorized)
            return unauthorized;
        var userId = User.GetCurrentUserId()!.Value;
        var answers = request.Answers.Select(a => new QuizAnswerInput(a.QuestionId, a.SubmittedAnswer)).ToList();
        var command = new SubmitQuizCommand(request.QuizId, userId, answers);
        var result = await _mediator.Send(command, cancellationToken);
        return result.ToActionResult(_correlationIdAccessor?.Get(), v => _mapper.Map<SubmitQuizResponse>(v));
    }
}
