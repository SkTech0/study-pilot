namespace StudyPilot.API.Contracts.Responses;

public sealed record AuthResponse(string AccessToken, string RefreshToken, DateTime ExpiresAtUtc, Guid UserId);
