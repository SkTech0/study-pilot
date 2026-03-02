using Microsoft.EntityFrameworkCore;
using StudyPilot.Application.Abstractions.Persistence;
using StudyPilot.Domain.Entities;
using StudyPilot.Domain.Enums;
using StudyPilot.Infrastructure.Persistence.DbContext;

namespace StudyPilot.Infrastructure.Persistence.Repositories;

public sealed class DocumentRepository : IDocumentRepository
{
    private readonly StudyPilotDbContext _db;

    public DocumentRepository(StudyPilotDbContext db) => _db = db;

    public async Task<Document?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _db.Documents
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Document>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await _db.Documents
            .AsNoTracking()
            .Where(d => d.UserId == userId)
            .OrderByDescending(d => d.CreatedAtUtc)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(Document document, CancellationToken cancellationToken = default) =>
        await _db.Documents.AddAsync(document, cancellationToken);

    public Task UpdateAsync(Document document, CancellationToken cancellationToken = default)
    {
        _db.Documents.Update(document);
        return Task.CompletedTask;
    }

    public async Task<Document?> TryClaimForProcessingAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var updated = await _db.Documents
            .Where(d => d.Id == documentId && d.ProcessingStatus == ProcessingStatus.Pending)
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.ProcessingStatus, ProcessingStatus.Processing), cancellationToken);
        if (updated == 0) return null;
        return await _db.Documents.FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken);
    }

    public async Task ResetToPendingAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        await _db.Documents
            .Where(d => d.Id == documentId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(d => d.ProcessingStatus, ProcessingStatus.Pending)
                .SetProperty(d => d.FailureReason, (string?)null), cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> GetStuckProcessingDocumentIdsAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default) =>
        await _db.Documents
            .AsNoTracking()
            .Where(d => d.ProcessingStatus == ProcessingStatus.Processing && d.UpdatedAtUtc < cutoffUtc)
            .Select(d => d.Id)
            .ToListAsync(cancellationToken);
}
