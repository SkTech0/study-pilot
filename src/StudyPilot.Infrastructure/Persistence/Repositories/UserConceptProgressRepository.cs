using Microsoft.EntityFrameworkCore;
using StudyPilot.Application.Abstractions.Persistence;
using StudyPilot.Domain.Entities;
using StudyPilot.Infrastructure.Persistence.DbContext;

namespace StudyPilot.Infrastructure.Persistence.Repositories;

public sealed class UserConceptProgressRepository : IUserConceptProgressRepository
{
    private readonly StudyPilotDbContext _db;

    public UserConceptProgressRepository(StudyPilotDbContext db) => _db = db;

    public async Task<UserConceptProgress?> GetByUserAndConceptAsync(Guid userId, Guid conceptId, CancellationToken cancellationToken = default)
    {
        var row = await _db.UserConceptProgresses
            .FirstOrDefaultAsync(p => p.UserId == userId && p.ConceptId == conceptId, cancellationToken);
        return row;
    }

    public async Task<IReadOnlyList<UserConceptProgress>> GetWeakByUserIdAsync(Guid userId, int threshold, CancellationToken cancellationToken = default)
    {
        // Raw SQL for filter so threshold (int) is not run through MasteryScore converter (avoids InvalidCastException).
        // Then load full entities by Id so the value converter is used only on materialization.
        var ids = await _db.Database
            .SqlQueryRaw<Guid>(
                "SELECT \"Id\" AS \"Value\" FROM \"UserConceptProgresses\" WHERE \"UserId\" = {0} AND \"MasteryScore\" < {1}",
                userId,
                threshold)
            .ToListAsync(cancellationToken);
        if (ids.Count == 0)
            return [];
        return await _db.UserConceptProgresses
            .AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(UserConceptProgress progress, CancellationToken cancellationToken = default) =>
        await _db.UserConceptProgresses.AddAsync(progress, cancellationToken);

    public Task UpdateAsync(UserConceptProgress progress, CancellationToken cancellationToken = default)
    {
        _db.UserConceptProgresses.Update(progress);
        return Task.CompletedTask;
    }
}
