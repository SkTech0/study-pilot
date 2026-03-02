using StudyPilot.Domain.Entities;

namespace StudyPilot.Application.Abstractions.Persistence;

public interface IChatMessageRepository
{
    Task AddAsync(ChatMessage message, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChatMessage>> GetBySessionIdAsync(Guid sessionId, int skip, int take, CancellationToken cancellationToken = default);
    Task<int> CountBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken = default);
}

