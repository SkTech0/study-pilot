using StudyPilot.Domain.Entities;

namespace StudyPilot.Application.Abstractions.Persistence;

public interface IChatSessionRepository
{
    Task<ChatSession?> GetByIdAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task AddAsync(ChatSession session, CancellationToken cancellationToken = default);
}

