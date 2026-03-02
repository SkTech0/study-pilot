using Microsoft.EntityFrameworkCore;
using StudyPilot.Application.Abstractions.Persistence;
using StudyPilot.Infrastructure.Persistence;
using StudyPilot.Infrastructure.Persistence.DbContext;

namespace StudyPilot.Infrastructure.Persistence.Repositories;

public sealed class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly StudyPilotDbContext _db;

    public RefreshTokenRepository(StudyPilotDbContext db) => _db = db;

    public async Task<(Guid UserId, DateTime ExpiresAtUtc)?> GetValidByTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var entity = await _db.RefreshTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Token == token && x.RevokedAtUtc == null && x.ExpiresAtUtc > now, cancellationToken);
        return entity == null ? null : (entity.UserId, entity.ExpiresAtUtc);
    }

    public async Task AddAsync(Guid userId, string token, DateTime expiresAtUtc, CancellationToken cancellationToken = default)
    {
        var entity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Token = token,
            ExpiresAtUtc = expiresAtUtc,
            CreatedAtUtc = DateTime.UtcNow
        };
        await _db.RefreshTokens.AddAsync(entity, cancellationToken);
    }

    public async Task RevokeByTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        var entity = await _db.RefreshTokens.FirstOrDefaultAsync(x => x.Token == token, cancellationToken);
        if (entity != null)
        {
            entity.RevokedAtUtc = DateTime.UtcNow;
        }
    }
}
