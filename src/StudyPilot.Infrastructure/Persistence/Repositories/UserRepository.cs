using Microsoft.EntityFrameworkCore;
using StudyPilot.Application.Abstractions.Persistence;
using StudyPilot.Domain.Entities;
using StudyPilot.Infrastructure.Persistence.DbContext;

namespace StudyPilot.Infrastructure.Persistence.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly StudyPilotDbContext _db;

    public UserRepository(StudyPilotDbContext db) => _db = db;

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalized = email.Trim();
        // Look up Id by raw SQL (string param not tied to Email value object), then load entity so
        // ValueConverter is only used on materialization, not on parameter (avoids InvalidCastException).
        // SqlQueryRaw<T> with T=Guid expects a column named "Value" in the result set.
        var id = await _db.Database
            .SqlQueryRaw<Guid>("SELECT \"Id\" AS \"Value\" FROM \"Users\" WHERE \"Email\" = {0}", normalized)
            .FirstOrDefaultAsync(cancellationToken);
        if (id == default)
            return null;
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        await _db.Users.AddAsync(user, cancellationToken);
    }
}
