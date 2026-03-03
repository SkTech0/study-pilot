using StudyPilot.Domain.Entities;

namespace StudyPilot.Application.Abstractions.Persistence;

public interface ILearningGoalRepository
{
    Task<IReadOnlyList<LearningGoal>> GetByTutorSessionIdAsync(Guid tutorSessionId, CancellationToken cancellationToken = default);
    Task<LearningGoal?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(LearningGoal goal, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<LearningGoal> goals, CancellationToken cancellationToken = default);
    Task UpdateAsync(LearningGoal goal, CancellationToken cancellationToken = default);
}
