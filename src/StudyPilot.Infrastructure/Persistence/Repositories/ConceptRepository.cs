using Microsoft.EntityFrameworkCore;
using StudyPilot.Application.Abstractions.Persistence;
using StudyPilot.Domain.Entities;
using StudyPilot.Infrastructure.Persistence.DbContext;

namespace StudyPilot.Infrastructure.Persistence.Repositories;

public sealed class ConceptRepository : IConceptRepository
{
    private readonly StudyPilotDbContext _db;

    public ConceptRepository(StudyPilotDbContext db) => _db = db;

    public async Task<Concept?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _db.Concepts
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Concept>> GetByDocumentIdAsync(Guid documentId, CancellationToken cancellationToken = default) =>
        await _db.Concepts
            .AsNoTracking()
            .Where(c => c.DocumentId == documentId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Concept>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        var idList = ids.ToList();
        if (idList.Count == 0) return [];
        return await _db.Concepts
            .AsNoTracking()
            .Where(c => idList.Contains(c.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Concept concept, CancellationToken cancellationToken = default) =>
        await _db.Concepts.AddAsync(concept, cancellationToken);

    public async Task AddRangeAsync(IEnumerable<Concept> concepts, CancellationToken cancellationToken = default) =>
        await _db.Concepts.AddRangeAsync(concepts, cancellationToken);

    public async Task DeleteByDocumentIdAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        await _db.Concepts.Where(c => c.DocumentId == documentId).ExecuteDeleteAsync(cancellationToken);
    }
}
