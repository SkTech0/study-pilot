using Microsoft.AspNetCore.Mvc;
using StudyPilot.API.Contracts;
using StudyPilot.Application.Common.Errors;
using StudyPilot.Application.Common.Models;

namespace StudyPilot.API.Extensions;

public static class ResultExtensions
{
    public static ActionResult<ApiResponse<TResponse>> ToActionResult<T, TResponse>(
        this Result<T> result,
        string? correlationId,
        Func<T, TResponse> mapSuccess)
        where TResponse : class?
    {
        if (result.IsSuccess)
            return new OkObjectResult(ApiResponse<TResponse>.Ok(mapSuccess(result.Value!), correlationId));

        var statusCode = result.Errors.Count > 0
            ? GetStatusCode(result.Errors[0])
            : 500;
        return new ObjectResult(ApiResponse<TResponse>.Fail(result.Errors, correlationId)) { StatusCode = statusCode };
    }

    private static int GetStatusCode(AppError error)
    {
        if (error.Code == ErrorCodes.AuthInvalidCredentials || error.Code == ErrorCodes.AuthInvalidToken ||
            error.Code == ErrorCodes.RefreshTokenInvalid)
            return 401;
        if (error.Code == ErrorCodes.RateLimitExceeded) return 429;
        if (error.Code == ErrorCodes.AiServiceUnavailable) return 503;
        return error.Severity switch
        {
            ErrorSeverity.Validation => 400,
            ErrorSeverity.Business => 409,
            _ => 500
        };
    }
}
