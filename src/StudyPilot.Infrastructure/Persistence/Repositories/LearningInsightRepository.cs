using Microsoft.EntityFrameworkCore;
using StudyPilot.Application.Abstractions.Persistence;
using StudyPilot.Domain.Entities;
using StudyPilot.Domain.Enums;
using StudyPilot.Infrastructure.Persistence.DbContext;

namespace StudyPilot.Infrastructure.Persistence.Repositories;

public sealed class LearningInsightRepository : ILearningInsightRepository
{
    private readonly StudyPilotDbContext _db;

    public LearningInsightRepository(StudyPilotDbContext db) => _db = db;

    public async Task AddAsync(LearningInsight insight, CancellationToken cancellationToken = default) =>
        await _db.LearningInsights.AddAsync(insight, cancellationToken);

    public async Task AddRangeAsync(IReadOnlyList<LearningInsight> insights, CancellationToken cancellationToken = default)
    {
        if (insights.Count == 0) return;
        await _db.LearningInsights.AddRangeAsync(insights, cancellationToken);
    }

    public async Task<IReadOnlyList<LearningInsight>> GetByUserIdAsync(Guid userId, int limit, CancellationToken cancellationToken = default) =>
        await _db.LearningInsights
            .AsNoTracking()
            .Where(i => i.UserId == userId)
            .OrderByDescending(i => i.CreatedUtc)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public async Task<bool> ExistsAsync(Guid userId, Guid conceptId, LearningInsightType type, DateTime sinceUtc, CancellationToken cancellationToken = default) =>
        await _db.LearningInsights
            .AnyAsync(i => i.UserId == userId && i.ConceptId == conceptId && i.InsightType == type && i.CreatedUtc >= sinceUtc, cancellationToken);
}
