using StudyPilot.Domain.Entities;

namespace StudyPilot.Application.Abstractions.Persistence;

public interface ITutorMessageRepository
{
    Task AddAsync(TutorMessage message, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TutorMessage>> GetBySessionIdAsync(Guid tutorSessionId, int limit, CancellationToken cancellationToken = default);
}
