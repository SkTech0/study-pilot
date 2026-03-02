namespace StudyPilot.Application.Abstractions.Auth;

public interface ITokenGenerator
{
    (string Token, DateTime ExpiresAtUtc) GenerateAccessToken(Guid userId, string email, string role);
    (string Token, DateTime ExpiresAtUtc) GenerateRefreshToken();
}
