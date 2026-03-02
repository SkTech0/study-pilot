using Microsoft.EntityFrameworkCore;
using StudyPilot.Application.Abstractions.Persistence;
using StudyPilot.Domain.Entities;
using StudyPilot.Infrastructure.Persistence.DbContext;

namespace StudyPilot.Infrastructure.Persistence.Repositories;

public sealed class ChatSessionRepository : IChatSessionRepository
{
    private readonly StudyPilotDbContext _db;

    public ChatSessionRepository(StudyPilotDbContext db) => _db = db;

    public async Task<ChatSession?> GetByIdAsync(Guid sessionId, CancellationToken cancellationToken = default) =>
        await _db.ChatSessions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);

    public async Task AddAsync(ChatSession session, CancellationToken cancellationToken = default) =>
        await _db.ChatSessions.AddAsync(session, cancellationToken);
}

