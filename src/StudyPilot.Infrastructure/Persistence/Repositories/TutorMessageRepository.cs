using Microsoft.EntityFrameworkCore;
using StudyPilot.Application.Abstractions.Persistence;
using StudyPilot.Domain.Entities;
using StudyPilot.Infrastructure.Persistence.DbContext;

namespace StudyPilot.Infrastructure.Persistence.Repositories;

public sealed class TutorMessageRepository : ITutorMessageRepository
{
    private readonly StudyPilotDbContext _db;

    public TutorMessageRepository(StudyPilotDbContext db) => _db = db;

    public async Task AddAsync(TutorMessage message, CancellationToken cancellationToken = default) =>
        await _db.TutorMessages.AddAsync(message, cancellationToken);

    public async Task<IReadOnlyList<TutorMessage>> GetBySessionIdAsync(Guid tutorSessionId, int limit, CancellationToken cancellationToken = default) =>
        await _db.TutorMessages
            .AsNoTracking()
            .Where(m => m.TutorSessionId == tutorSessionId)
            .OrderByDescending(m => m.CreatedUtc)
            .Take(limit)
            .ToListAsync(cancellationToken);
}
