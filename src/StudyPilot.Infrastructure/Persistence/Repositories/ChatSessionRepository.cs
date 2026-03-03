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

    public async Task<int> DeleteStaleSessionsAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default)
    {
        var staleIds = await _db.ChatSessions
            .Where(s => s.UpdatedAtUtc < cutoffUtc)
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);
        if (staleIds.Count == 0) return 0;
        var messages = await _db.ChatMessages.Where(m => staleIds.Contains(m.SessionId)).ToListAsync(cancellationToken);
        var msgIds = messages.Select(m => m.Id).ToList();
        if (msgIds.Count > 0)
        {
            var citations = await _db.ChatMessageCitations.Where(c => msgIds.Contains(c.MessageId)).ToListAsync(cancellationToken);
            _db.ChatMessageCitations.RemoveRange(citations);
            _db.ChatMessages.RemoveRange(messages);
        }
        var sessions = await _db.ChatSessions.Where(s => staleIds.Contains(s.Id)).ToListAsync(cancellationToken);
        _db.ChatSessions.RemoveRange(sessions);
        await _db.SaveChangesAsync(cancellationToken);
        return sessions.Count;
    }
}

