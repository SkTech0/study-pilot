using Microsoft.EntityFrameworkCore;
using StudyPilot.Application.Abstractions.Persistence;
using StudyPilot.Domain.Entities;
using StudyPilot.Infrastructure.Persistence.DbContext;

namespace StudyPilot.Infrastructure.Persistence.Repositories;

public sealed class ChatMessageRepository : IChatMessageRepository
{
    private readonly StudyPilotDbContext _db;

    public ChatMessageRepository(StudyPilotDbContext db) => _db = db;

    public async Task AddAsync(ChatMessage message, CancellationToken cancellationToken = default) =>
        await _db.ChatMessages.AddAsync(message, cancellationToken);

    public async Task<IReadOnlyList<ChatMessage>> GetBySessionIdAsync(Guid sessionId, int skip, int take, CancellationToken cancellationToken = default) =>
        await _db.ChatMessages
            .AsNoTracking()
            .Where(m => m.SessionId == sessionId)
            .OrderBy(m => m.CreatedAtUtc)
            .ThenBy(m => m.Id)
            .Skip(Math.Max(0, skip))
            .Take(Math.Max(1, take))
            .ToListAsync(cancellationToken);

    public async Task<int> CountBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken = default) =>
        await _db.ChatMessages.AsNoTracking().CountAsync(m => m.SessionId == sessionId, cancellationToken);
}

