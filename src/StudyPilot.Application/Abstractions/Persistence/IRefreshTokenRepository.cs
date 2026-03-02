namespace StudyPilot.Application.Abstractions.Persistence;

public interface IRefreshTokenRepository
{
    Task<(Guid UserId, DateTime ExpiresAtUtc)?> GetValidByTokenAsync(string token, CancellationToken cancellationToken = default);
    Task AddAsync(Guid userId, string token, DateTime expiresAtUtc, CancellationToken cancellationToken = default);
    Task RevokeByTokenAsync(string token, CancellationToken cancellationToken = default);
}
