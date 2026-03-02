using StudyPilot.Application.Abstractions.Persistence;
using StudyPilot.Infrastructure.Persistence.DbContext;

namespace StudyPilot.Infrastructure.Persistence;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly StudyPilotDbContext _db;

    public UnitOfWork(StudyPilotDbContext db) => _db = db;

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _db.SaveChangesAsync(cancellationToken);
}
