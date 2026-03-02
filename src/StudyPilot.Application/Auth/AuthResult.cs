namespace StudyPilot.Application.Auth;

public sealed record AuthResult(string AccessToken, string RefreshToken, DateTime AccessTokenExpiresAtUtc, Guid UserId);
