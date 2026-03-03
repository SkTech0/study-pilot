using Microsoft.EntityFrameworkCore;
using StudyPilot.Application.Abstractions.Persistence;
using StudyPilot.Domain.Entities;
using StudyPilot.Infrastructure.Persistence.DbContext;

namespace StudyPilot.Infrastructure.Persistence.Repositories;

public sealed class LearningGoalRepository : ILearningGoalRepository
{
    private readonly StudyPilotDbContext _db;

    public LearningGoalRepository(StudyPilotDbContext db) => _db = db;

    public async Task<IReadOnlyList<LearningGoal>> GetByTutorSessionIdAsync(Guid tutorSessionId, CancellationToken cancellationToken = default) =>
        await _db.LearningGoals
            .AsNoTracking()
            .Where(g => g.TutorSessionId == tutorSessionId)
            .OrderBy(g => g.Priority)
            .ToListAsync(cancellationToken);

    public async Task<LearningGoal?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _db.LearningGoals.FirstOrDefaultAsync(g => g.Id == id, cancellationToken);

    public async Task AddAsync(LearningGoal goal, CancellationToken cancellationToken = default) =>
        await _db.LearningGoals.AddAsync(goal, cancellationToken);

    public async Task AddRangeAsync(IEnumerable<LearningGoal> goals, CancellationToken cancellationToken = default) =>
        await _db.LearningGoals.AddRangeAsync(goals, cancellationToken);

    public Task UpdateAsync(LearningGoal goal, CancellationToken cancellationToken = default)
    {
        _db.LearningGoals.Update(goal);
        return Task.CompletedTask;
    }
}
