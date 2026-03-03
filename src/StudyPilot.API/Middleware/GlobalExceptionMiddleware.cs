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
        var start = DateTime.UtcNow;
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
                correlationId,
                FailureCategory.ValidationFailure)).ToList();
            LogError(context, errors[0].Code, FailureCategory.ValidationFailure, null, (DateTime.UtcNow - start).TotalMilliseconds, ex, correlationId);
            await WriteErrorResponse(context, (int)HttpStatusCode.BadRequest, errors, correlationId);
        }
        catch (DomainException ex)
        {
            var correlationId = _correlationIdAccessor?.Get();
            var errors = ex.Errors.Select(e => new AppError(e.Code, e.Message, e.Field, ErrorSeverity.Business, correlationId, FailureCategory.ConsistencyFailure)).ToList();
            LogError(context, errors[0].Code, FailureCategory.ConsistencyFailure, null, (DateTime.UtcNow - start).TotalMilliseconds, ex, correlationId);
            await WriteErrorResponse(context, (int)HttpStatusCode.Conflict, errors, correlationId);
        }
        catch (UnauthorizedAccessException ex)
        {
            var correlationId = _correlationIdAccessor?.Get();
            var errors = new List<AppError> { new(ErrorCodes.AuthInvalidToken, "Unauthorized.", null, ErrorSeverity.System, correlationId, FailureCategory.ValidationFailure) };
            LogError(context, ErrorCodes.AuthInvalidToken, FailureCategory.ValidationFailure, null, (DateTime.UtcNow - start).TotalMilliseconds, ex, correlationId);
            await WriteErrorResponse(context, (int)HttpStatusCode.Unauthorized, errors, correlationId);
        }
        catch (HttpRequestException ex)
        {
            var correlationId = _correlationIdAccessor?.Get();
            var errors = new List<AppError> { new(ErrorCodes.AiServiceUnavailable, "AI service is temporarily unavailable.", null, ErrorSeverity.System, correlationId, FailureCategory.DependencyUnavailable) };
            LogError(context, ErrorCodes.AiServiceUnavailable, FailureCategory.DependencyUnavailable, "AIService", (DateTime.UtcNow - start).TotalMilliseconds, ex, correlationId);
            await WriteErrorResponse(context, (int)HttpStatusCode.ServiceUnavailable, errors, correlationId);
        }
        catch (OperationCanceledException ex)
        {
            var correlationId = _correlationIdAccessor?.Get();
            var errors = new List<AppError> { new(ErrorCodes.UnexpectedError, "Request was cancelled or timed out.", null, ErrorSeverity.System, correlationId, FailureCategory.TimeoutFailure) };
            LogError(context, "RequestCancelled", FailureCategory.TimeoutFailure, null, (DateTime.UtcNow - start).TotalMilliseconds, ex, correlationId);
            await WriteErrorResponse(context, 499, errors, correlationId);
        }
        catch (Exception ex)
        {
            var correlationId = _correlationIdAccessor?.Get() ?? "";
            var category = FailureClassifier.Classify(ex);
            var message = "An unexpected error occurred.";
            var errors = new List<AppError> { new(ErrorCodes.UnexpectedError, message, null, ErrorSeverity.System, correlationId, category) };
            LogError(context, ErrorCodes.UnexpectedError, category, null, (DateTime.UtcNow - start).TotalMilliseconds, ex, correlationId);
            await WriteErrorResponse(context, (int)HttpStatusCode.InternalServerError, errors, correlationId);
        }
    }

    private void LogError(HttpContext context, string errorCode, FailureCategory category, string? dependencyName, double elapsedMs, Exception ex, string? correlationId)
    {
        var userId = context.User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.User?.FindFirstValue("sub");
        var operationName = $"{context.Request.Method} {context.Request.Path}";
        _logger.LogError(ex,
            "ErrorCode={ErrorCode} FailureCategory={FailureCategory} CorrelationId={CorrelationId} DependencyName={DependencyName} OperationName={OperationName} ElapsedMs={ElapsedMs} UserId={UserId}",
            errorCode, category, correlationId ?? "", dependencyName ?? "", operationName, elapsedMs, userId ?? "");
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
