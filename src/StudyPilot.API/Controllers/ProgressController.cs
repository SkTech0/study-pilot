using AutoMapper;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyPilot.API.Contracts;
using StudyPilot.API.Contracts.Responses;
using StudyPilot.API.Extensions;
using StudyPilot.Application.Abstractions.Observability;
using StudyPilot.Application.Progress.GetWeakConcepts;

namespace StudyPilot.API.Controllers;

[ApiController]
[Route("progress")]
[Authorize]
public sealed class ProgressController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IMapper _mapper;
    private readonly ICorrelationIdAccessor? _correlationIdAccessor;

    public ProgressController(IMediator mediator, IMapper mapper, ICorrelationIdAccessor? correlationIdAccessor = null)
    {
        _mediator = mediator;
        _mapper = mapper;
        _correlationIdAccessor = correlationIdAccessor;
    }

    [HttpGet("weak-topics")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<WeakTopicResponse>>>> GetWeakTopics(CancellationToken cancellationToken)
    {
        if (this.UnauthorizedIfNoUser<IReadOnlyList<WeakTopicResponse>>(_correlationIdAccessor) is { } unauthorized)
            return unauthorized;

        var userId = User.GetCurrentUserId()!.Value;
        var query = new GetWeakConceptsQuery(userId);
        var result = await _mediator.Send(query, cancellationToken);
        return result.ToActionResult(_correlationIdAccessor?.Get(), list => (IReadOnlyList<WeakTopicResponse>)list!.Select(_mapper.Map<WeakTopicResponse>).ToList());
    }
}
