using Microsoft.EntityFrameworkCore;
using StudyPilot.Application.Abstractions.Persistence;
using StudyPilot.Domain.Entities;
using StudyPilot.Infrastructure.Persistence.DbContext;

namespace StudyPilot.Infrastructure.Persistence.Repositories;

public sealed class UserConceptMasteryRepository : IUserConceptMasteryRepository
{
    private readonly StudyPilotDbContext _db;

    public UserConceptMasteryRepository(StudyPilotDbContext db) => _db = db;

    public async Task<UserConceptMastery?> GetByUserAndConceptAsync(Guid userId, Guid conceptId, CancellationToken cancellationToken = default) =>
        await _db.UserConceptMasteries
            .FirstOrDefaultAsync(m => m.UserId == userId && m.ConceptId == conceptId, cancellationToken);

    public async Task<IReadOnlyList<UserConceptMastery>> GetByUserAndConceptsAsync(Guid userId, IReadOnlyList<Guid> conceptIds, CancellationToken cancellationToken = default)
    {
        if (conceptIds.Count == 0) return Array.Empty<UserConceptMastery>();
        return await _db.UserConceptMasteries
            .AsNoTracking()
            .Where(m => m.UserId == userId && conceptIds.Contains(m.ConceptId))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<UserConceptMastery>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await _db.UserConceptMasteries
            .Where(m => m.UserId == userId)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(UserConceptMastery entity, CancellationToken cancellationToken = default) =>
        await _db.UserConceptMasteries.AddAsync(entity, cancellationToken);

    public Task UpdateAsync(UserConceptMastery entity, CancellationToken cancellationToken = default)
    {
        _db.UserConceptMasteries.Update(entity);
        return Task.CompletedTask;
    }

    public async Task UpsertBatchAsync(IReadOnlyList<UserConceptMastery> entities, CancellationToken cancellationToken = default)
    {
        foreach (var e in entities)
        {
            var existing = await _db.UserConceptMasteries
                .FirstOrDefaultAsync(m => m.UserId == e.UserId && m.ConceptId == e.ConceptId, cancellationToken);
            if (existing is null)
                await _db.UserConceptMasteries.AddAsync(e, cancellationToken);
            else
                _db.UserConceptMasteries.Update(e);
        }
    }

    public async Task<IReadOnlyList<Guid>> GetDistinctUserIdsAsync(CancellationToken cancellationToken = default) =>
        await _db.UserConceptMasteries.Select(m => m.UserId).Distinct().ToListAsync(cancellationToken);
}
