using AutoMapper;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyPilot.API.Contracts;
using StudyPilot.API.Contracts.Responses;
using StudyPilot.API.Extensions;
using StudyPilot.Application.Abstractions.Observability;
using StudyPilot.Application.Learning.GetLearningOverview;
using StudyPilot.Application.Learning.GetLearningProgress;
using StudyPilot.Application.Learning.GetStudySuggestions;
using StudyPilot.Application.Learning.GetLearningWeakTopics;

namespace StudyPilot.API.Controllers;

[ApiController]
[Route("learning")]
[Authorize]
public sealed class LearningController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IMapper _mapper;
    private readonly ICorrelationIdAccessor? _correlationIdAccessor;

    public LearningController(IMediator mediator, IMapper mapper, ICorrelationIdAccessor? correlationIdAccessor = null)
    {
        _mediator = mediator;
        _mapper = mapper;
        _correlationIdAccessor = correlationIdAccessor;
    }

    [HttpGet("overview")]
    public async Task<ActionResult<ApiResponse<LearningOverviewResponse>>> GetOverview(CancellationToken cancellationToken)
    {
        if (this.UnauthorizedIfNoUser<LearningOverviewResponse>(_correlationIdAccessor) is { } unauthorized)
            return unauthorized;
        var userId = User.GetCurrentUserId()!.Value;
        var result = await _mediator.Send(new GetLearningOverviewQuery(userId), cancellationToken);
        return result.ToActionResult(_correlationIdAccessor?.Get(), v => _mapper.Map<LearningOverviewResponse>(v));
    }

    [HttpGet("weak-topics")]
    public async Task<ActionResult<ApiResponse<LearningWeakTopicsResponse>>> GetWeakTopics([FromQuery] int maxCount = 20, CancellationToken cancellationToken = default)
    {
        if (this.UnauthorizedIfNoUser<LearningWeakTopicsResponse>(_correlationIdAccessor) is { } unauthorized)
            return unauthorized;
        var userId = User.GetCurrentUserId()!.Value;
        var result = await _mediator.Send(new GetLearningWeakTopicsQuery(userId, Math.Clamp(maxCount, 1, 100)), cancellationToken);
        return result.ToActionResult(_correlationIdAccessor?.Get(), v => _mapper.Map<LearningWeakTopicsResponse>(v));
    }

    [HttpGet("progress")]
    public async Task<ActionResult<ApiResponse<LearningProgressResponse>>> GetProgress(CancellationToken cancellationToken)
    {
        if (this.UnauthorizedIfNoUser<LearningProgressResponse>(_correlationIdAccessor) is { } unauthorized)
            return unauthorized;
        var userId = User.GetCurrentUserId()!.Value;
        var result = await _mediator.Send(new GetLearningProgressQuery(userId), cancellationToken);
        return result.ToActionResult(_correlationIdAccessor?.Get(), v => _mapper.Map<LearningProgressResponse>(v));
    }

    [HttpGet("suggestions")]
    public async Task<ActionResult<ApiResponse<StudySuggestionsResponse>>> GetSuggestions(CancellationToken cancellationToken)
    {
        if (this.UnauthorizedIfNoUser<StudySuggestionsResponse>(_correlationIdAccessor) is { } unauthorized)
            return unauthorized;
        var userId = User.GetCurrentUserId()!.Value;
        var result = await _mediator.Send(new GetStudySuggestionsQuery(userId), cancellationToken);
        return result.ToActionResult(_correlationIdAccessor?.Get(), v => _mapper.Map<StudySuggestionsResponse>(v));
    }
}
