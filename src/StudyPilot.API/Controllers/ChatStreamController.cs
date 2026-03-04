using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using StudyPilot.API.Extensions;
using StudyPilot.Application.Abstractions.Observability;
using StudyPilot.Application.Chat;
using StudyPilot.Application.Chat.StreamChatMessage;

namespace StudyPilot.API.Controllers;

[ApiController]
[Route("chat")]
[Authorize]
[EnableRateLimiting("chat-policy")]
public sealed class ChatStreamController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICorrelationIdAccessor? _correlationIdAccessor;

    public ChatStreamController(IMediator mediator, ICorrelationIdAccessor? correlationIdAccessor = null)
    {
        _mediator = mediator;
        _correlationIdAccessor = correlationIdAccessor;
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
        var query = new StreamChatMessageQuery(userId, sessionId, message?.Trim() ?? "");
        var result = await _mediator.Send(query, cancellationToken);

        if (!result.IsSuccess)
        {
            Response.StatusCode = result.Errors.Count > 0
                ? GetStatusCode(result.Errors[0])
                : 500;
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
            await Response.WriteAsync("event: start\ndata: {}\n\n", Encoding.UTF8, cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
            await foreach (var token in streamResult.Tokens.WithCancellation(cancellationToken))
            {
                var payload = EscapeSseData(token);
                await Response.WriteAsync($"event: token\ndata: {payload}\n\n", Encoding.UTF8, cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }
            await streamResult.WhenComplete.WaitAsync(cancellationToken);
            var status = await streamResult.StatusTask.WaitAsync(cancellationToken);
            var doneData = JsonSerializer.Serialize(new { status = status.ToApiString() });
            await Response.WriteAsync($"event: done\ndata: {doneData}\n\n", Encoding.UTF8, cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Client disconnect; streaming stopped cleanly
        }
    }

    private static string EscapeSseData(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);
    }

    private static int GetStatusCode(Application.Common.Errors.AppError error)
    {
        if (error.Code == Application.Common.Errors.ErrorCodes.ChatSessionNotFound) return 404;
        if (error.Code == Application.Common.Errors.ErrorCodes.ChatSessionAccessDenied) return 403;
        return 500;
    }
}
