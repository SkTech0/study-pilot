using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyPilot.API.Contracts;
using StudyPilot.API.Extensions;
using StudyPilot.Application.Abstractions.Observability;
using System.Text;
using System.Text.Json;
using StudyPilot.Application.Tutor.EvaluateExercise;
using StudyPilot.Application.Tutor.StartTutorSession;
using StudyPilot.Application.Tutor.StreamTutor;
using StudyPilot.Application.Tutor.TutorRespond;
using StudyPilot.Infrastructure.Services;

namespace StudyPilot.API.Controllers;

[ApiController]
[Route("tutor")]
[Authorize]
public sealed class TutorController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICorrelationIdAccessor? _correlationIdAccessor;

    public TutorController(IMediator mediator, ICorrelationIdAccessor? correlationIdAccessor = null)
    {
        _mediator = mediator;
        _correlationIdAccessor = correlationIdAccessor;
    }

    [HttpPost("start")]
    public async Task<ActionResult<ApiResponse<StartTutorApiResponse>>> Start(
        [FromBody] StartTutorRequest request,
        CancellationToken cancellationToken)
    {
        if (this.UnauthorizedIfNoUser<StartTutorApiResponse>(_correlationIdAccessor) is { } unauthorized)
            return unauthorized;
        var userId = User.GetCurrentUserId()!.Value;
        var result = await _mediator.Send(new StartTutorSessionCommand(userId, request.DocumentId), cancellationToken);
        if (!result.IsSuccess)
            return result.ToActionResult(_correlationIdAccessor?.Get(), _ => (StartTutorApiResponse)null!);
        StudyPilotMetrics.TutorSessionStarted.Add(1);
        return result.ToActionResult(_correlationIdAccessor?.Get(), v => new StartTutorApiResponse(v.SessionId, v.Goals.Select(g => new StartTutorGoalItem(g.GoalId, g.ConceptId, g.ConceptName, g.GoalType, g.Priority)).ToList()));
    }

    [HttpPost("respond")]
    public async Task<ActionResult<ApiResponse<TutorRespondApiResponse>>> Respond(
        [FromBody] TutorRespondRequest request,
        CancellationToken cancellationToken)
    {
        if (this.UnauthorizedIfNoUser<TutorRespondApiResponse>(_correlationIdAccessor) is { } unauthorized)
            return unauthorized;
        var userId = User.GetCurrentUserId()!.Value;
        var result = await _mediator.Send(new TutorRespondCommand(userId, request.SessionId, request.Message ?? ""), cancellationToken);
        if (!result.IsSuccess)
            return result.ToActionResult(_correlationIdAccessor?.Get(), _ => (TutorRespondApiResponse)null!);
        var v = result.Value!;
        if (v.OptionalExercise != null)
            StudyPilotMetrics.TutorExerciseGenerated.Add(1);
        StudyPilotMetrics.TutorStepTransition.Add(1);
        return result.ToActionResult(_correlationIdAccessor?.Get(), _ => new TutorRespondApiResponse(v.AssistantMessage, v.NextStep, v.OptionalExercise != null ? new TutorExerciseItem(v.OptionalExercise.ExerciseId, v.OptionalExercise.Question, v.OptionalExercise.ExpectedAnswer, v.OptionalExercise.Difficulty) : null, v.CitedChunkIds));
    }

    [HttpPost("evaluate-exercise")]
    public async Task<ActionResult<ApiResponse<EvaluateExerciseApiResponse>>> EvaluateExercise(
        [FromBody] EvaluateExerciseRequest request,
        CancellationToken cancellationToken)
    {
        if (this.UnauthorizedIfNoUser<EvaluateExerciseApiResponse>(_correlationIdAccessor) is { } unauthorized)
            return unauthorized;
        var userId = User.GetCurrentUserId()!.Value;
        var result = await _mediator.Send(new EvaluateExerciseCommand(userId, request.ExerciseId, request.UserAnswer ?? ""), cancellationToken);
        if (!result.IsSuccess)
            return result.ToActionResult(_correlationIdAccessor?.Get(), _ => (EvaluateExerciseApiResponse)null!);
        StudyPilotMetrics.TutorExerciseEvaluated.Add(1);
        return result.ToActionResult(_correlationIdAccessor?.Get(), v => new EvaluateExerciseApiResponse(v.IsCorrect, v.Explanation));
    }

    [HttpGet("stream")]
    public async Task Stream(
        [FromQuery] Guid sessionId,
        [FromQuery] string? message,
        CancellationToken cancellationToken)
    {
        if (this.UnauthorizedIfNoUser<object>(_correlationIdAccessor) is { } unauthorized)
        {
            await unauthorized.ExecuteResultAsync(ControllerContext);
            return;
        }
        var userId = User.GetCurrentUserId()!.Value;
        var result = await _mediator.Send(new StreamTutorQuery(userId, sessionId, message?.Trim() ?? ""), cancellationToken);
        if (!result.IsSuccess)
        {
            Response.StatusCode = result.Errors.Count > 0 ? ResultExtensions.GetStatusCodeForError(result.Errors[0]) : 500;
            await Response.WriteAsJsonAsync(new { errors = result.Errors }, cancellationToken);
            return;
        }
        var streamResult = result.Value!;
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";
        await Response.StartAsync(cancellationToken);
        try
        {
            await foreach (var token in streamResult.Tokens.WithCancellation(cancellationToken))
            {
                var payload = EscapeSseData(token);
                await Response.WriteAsync($"event: token\ndata: {payload}\n\n", Encoding.UTF8, cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }
            var done = new
            {
                nextStep = streamResult.NextStep,
                optionalExercise = streamResult.OptionalExercise == null ? null : new
                {
                    exerciseId = streamResult.OptionalExercise.ExerciseId,
                    question = streamResult.OptionalExercise.Question,
                    expectedAnswer = streamResult.OptionalExercise.ExpectedAnswer,
                    difficulty = streamResult.OptionalExercise.Difficulty
                },
                citedChunkIds = streamResult.CitedChunkIds
            };
            await Response.WriteAsync($"event: done\ndata: {JsonSerializer.Serialize(done)}\n\n", Encoding.UTF8, cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
            await streamResult.WhenComplete.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException) { }
    }

    private static string EscapeSseData(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.Replace("\r", "\\r", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal);
    }
}

public sealed record StartTutorApiResponse(Guid SessionId, IReadOnlyList<StartTutorGoalItem> Goals);
public sealed record StartTutorGoalItem(Guid GoalId, Guid ConceptId, string ConceptName, string GoalType, int Priority);
public sealed record TutorRespondApiResponse(string AssistantMessage, string NextStep, TutorExerciseItem? OptionalExercise, IReadOnlyList<Guid> CitedChunkIds);
public sealed record TutorExerciseItem(Guid ExerciseId, string Question, string ExpectedAnswer, string Difficulty);
public sealed record EvaluateExerciseApiResponse(bool IsCorrect, string Explanation);

public sealed class StartTutorRequest
{
    public Guid? DocumentId { get; set; }
}

public sealed class TutorRespondRequest
{
    public Guid SessionId { get; set; }
    public string? Message { get; set; }
}

public sealed class EvaluateExerciseRequest
{
    public Guid ExerciseId { get; set; }
    public string? UserAnswer { get; set; }
}
