using Microsoft.EntityFrameworkCore;
using StudyPilot.Application.Abstractions.Persistence;
using StudyPilot.Domain.Entities;
using StudyPilot.Infrastructure.Persistence.DbContext;

namespace StudyPilot.Infrastructure.Persistence.Repositories;

public sealed class PgVectorEmbeddingRepository : IDocumentChunkRepository
{
    private readonly StudyPilotDbContext _db;

    public PgVectorEmbeddingRepository(StudyPilotDbContext db) => _db = db;

    public async Task DeleteByDocumentIdAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        await _db.DocumentChunks.Where(c => c.DocumentId == documentId).ExecuteDeleteAsync(cancellationToken);
    }

    public async Task AddRangeAsync(IReadOnlyList<DocumentChunk> chunks, CancellationToken cancellationToken = default)
    {
        if (chunks.Count == 0) return;
        await _db.DocumentChunks.AddRangeAsync(chunks, cancellationToken);
    }
}

