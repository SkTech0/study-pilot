using Microsoft.EntityFrameworkCore;
using StudyPilot.Application.Abstractions.Persistence;
using StudyPilot.Domain.Entities;
using StudyPilot.Infrastructure.Persistence.DbContext;

namespace StudyPilot.Infrastructure.Persistence.Repositories;

public sealed class TutorSessionRepository : ITutorSessionRepository
{
    private readonly StudyPilotDbContext _db;

    public TutorSessionRepository(StudyPilotDbContext db) => _db = db;

    public async Task<TutorSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _db.TutorSessions.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task<TutorSession?> GetByIdAndUserIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default) =>
        await _db.TutorSessions.FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId, cancellationToken);

    public async Task AddAsync(TutorSession session, CancellationToken cancellationToken = default) =>
        await _db.TutorSessions.AddAsync(session, cancellationToken);

    public Task UpdateAsync(TutorSession session, CancellationToken cancellationToken = default)
    {
        _db.TutorSessions.Update(session);
        return Task.CompletedTask;
    }
}
