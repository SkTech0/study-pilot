using System.Net;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentValidation;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using StudyPilot.Application.Abstractions.Observability;
using StudyPilot.Application.Common.Errors;

namespace StudyPilot.API.Middleware;

public sealed class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ICorrelationIdAccessor? _correlationIdAccessor;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IWebHostEnvironment _env;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger, IWebHostEnvironment env, ICorrelationIdAccessor? correlationIdAccessor = null)
    {
        _next = next;
        _logger = logger;
        _env = env;
        _correlationIdAccessor = correlationIdAccessor;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ValidationException ex)
        {
            var correlationId = _correlationIdAccessor?.Get();
            var errors = ex.Errors.Select(f => new AppError(
                string.IsNullOrEmpty(f.ErrorCode) ? ErrorCodes.ValidationFailed : f.ErrorCode,
                f.ErrorMessage,
                ToCamelCase(f.PropertyName),
                ErrorSeverity.Validation,
                correlationId)).ToList();
            LogError(context, errors[0].Code, ex, correlationId);
            await WriteErrorResponse(context, (int)HttpStatusCode.BadRequest, errors, correlationId);
        }
        catch (DomainException ex)
        {
            var correlationId = _correlationIdAccessor?.Get();
            var errors = ex.Errors.Select(e => new AppError(e.Code, e.Message, e.Field, ErrorSeverity.Business, correlationId)).ToList();
            LogError(context, errors[0].Code, ex, correlationId);
            await WriteErrorResponse(context, (int)HttpStatusCode.Conflict, errors, correlationId);
        }
        catch (UnauthorizedAccessException ex)
        {
            var correlationId = _correlationIdAccessor?.Get();
            var errors = new List<AppError> { new(ErrorCodes.AuthInvalidToken, "Unauthorized.", null, ErrorSeverity.System, correlationId) };
            LogError(context, ErrorCodes.AuthInvalidToken, ex, correlationId);
            await WriteErrorResponse(context, (int)HttpStatusCode.Unauthorized, errors, correlationId);
        }
        catch (HttpRequestException ex)
        {
            var correlationId = _correlationIdAccessor?.Get();
            var errors = new List<AppError> { new(ErrorCodes.AiServiceUnavailable, "AI service is temporarily unavailable.", null, ErrorSeverity.System, correlationId) };
            LogError(context, ErrorCodes.AiServiceUnavailable, ex, correlationId);
            await WriteErrorResponse(context, (int)HttpStatusCode.ServiceUnavailable, errors, correlationId);
        }
        catch (Exception ex)
        {
            var correlationId = _correlationIdAccessor?.Get() ?? "";
            var errors = new List<AppError> { new(ErrorCodes.UnexpectedError, "An unexpected error occurred.", null, ErrorSeverity.System, correlationId) };
            LogError(context, ErrorCodes.UnexpectedError, ex, correlationId);
            await WriteErrorResponse(context, (int)HttpStatusCode.InternalServerError, errors, correlationId);
        }
    }

    private void LogError(HttpContext context, string errorCode, Exception ex, string? correlationId)
    {
        var userId = context.User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.User?.FindFirstValue("sub");
        _logger.LogError(ex, "ErrorCode={ErrorCode} CorrelationId={CorrelationId} UserId={UserId} Path={Path}",
            errorCode, correlationId ?? "", userId ?? "", context.Request.Path);
    }

    private static string? ToCamelCase(string? name)
    {
        if (string.IsNullOrEmpty(name) || name.Length == 1) return string.IsNullOrEmpty(name) ? null : char.ToLowerInvariant(name[0]).ToString();
        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    private static async Task WriteErrorResponse(HttpContext context, int statusCode, IReadOnlyList<AppError> errors, string? correlationId)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        var body = new { success = false, errors, correlationId };
        await context.Response.WriteAsync(JsonSerializer.Serialize(body, JsonOptions));
    }
}
