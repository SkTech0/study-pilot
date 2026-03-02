using Microsoft.AspNetCore.Mvc;
using StudyPilot.API.Contracts;
using StudyPilot.Application.Abstractions.Observability;
using StudyPilot.Application.Common.Errors;

namespace StudyPilot.API.Extensions;

/// <summary>
/// Extensions for returning consistent 401 API responses when the current user cannot be resolved.
/// </summary>
public static class ControllerBaseExtensions
{
    /// <summary>
    /// Returns 401 Unauthorized with ApiResponse.Fail when the current user ID cannot be resolved from claims.
    /// Use after [Authorize] when the endpoint requires a valid user Id.
    /// </summary>
    public static ObjectResult? UnauthorizedIfNoUser<TResponse>(
        this ControllerBase controller,
        ICorrelationIdAccessor? correlationIdAccessor)
        where TResponse : class
    {
        var userId = controller.User.GetCurrentUserId();
        if (userId.HasValue)
            return null;
        var correlationId = correlationIdAccessor?.Get();
        var errors = new[] { new AppError(ErrorCodes.AuthInvalidToken, "Invalid user.", null, ErrorSeverity.System, correlationId) };
        return new ObjectResult(ApiResponse<TResponse>.Fail(errors, correlationId)) { StatusCode = 401 };
    }
}
