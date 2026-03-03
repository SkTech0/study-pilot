using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
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
        var conn = (NpgsqlConnection)_db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            await conn.OpenAsync(cancellationToken);
        const string sql = @"UPDATE ""Documents"" SET ""ProcessingStatus"" = 'Processing' WHERE ""Id"" = @id AND ""ProcessingStatus"" = 'Pending' RETURNING ""Id"", ""UserId"", ""FileName"", ""StoragePath"", ""ProcessingStatus"", ""CreatedAtUtc"", ""UpdatedAtUtc"", ""FailureReason""";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", documentId);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        var statusStr = reader.GetString(4);
        var status = Enum.Parse<ProcessingStatus>(statusStr);
        return new Document(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetString(2),
            reader.GetString(3),
            status,
            reader.GetDateTime(5),
            reader.GetDateTime(6),
            reader.IsDBNull(7) ? null : reader.GetString(7));
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

    public async Task<int> ResetFailedDocumentsToPendingAsync(CancellationToken cancellationToken = default)
    {
        return await _db.Documents
            .Where(d => d.ProcessingStatus == ProcessingStatus.Failed)
            .ExecuteUpdateAsync(s => s
                .SetProperty(d => d.ProcessingStatus, ProcessingStatus.Pending)
                .SetProperty(d => d.FailureReason, (string?)null), cancellationToken);
    }
}
