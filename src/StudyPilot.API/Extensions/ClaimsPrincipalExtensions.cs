using System.Security.Claims;
using StudyPilot.API.Contracts;
using StudyPilot.Application.Common.Errors;

namespace StudyPilot.API.Extensions;

/// <summary>
/// Extensions for resolving the current authenticated user from claims.
/// Centralizes claim keys (NameIdentifier, "sub") used by JWT bearer auth.
/// </summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Gets the current user's ID from claims if present and valid Guid.
    /// </summary>
    public static Guid? GetCurrentUserId(this ClaimsPrincipal? principal)
    {
        if (principal?.Identity?.IsAuthenticated != true)
            return null;
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub");
        return Guid.TryParse(value, out var id) ? id : null;
    }
}
