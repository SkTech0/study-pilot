using StudyPilot.Domain.Entities;

namespace StudyPilot.Application.Abstractions.Persistence;

public interface ITutorSessionRepository
{
    Task<TutorSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TutorSession?> GetByIdAndUserIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(TutorSession session, CancellationToken cancellationToken = default);
    Task UpdateAsync(TutorSession session, CancellationToken cancellationToken = default);
}
