using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using StudyPilot.Application.Abstractions.Knowledge;
using StudyPilot.Application.Abstractions.Persistence;
using StudyPilot.Domain.Entities;
using StudyPilot.Domain.Enums;
using StudyPilot.Infrastructure.Persistence.DbContext;

namespace StudyPilot.Infrastructure.Persistence.Repositories;

public sealed class DocumentRepository : IDocumentRepository
{
    private readonly StudyPilotDbContext _db;
    private readonly IKnowledgeStateMachine _stateMachine;

    public DocumentRepository(StudyPilotDbContext db, IKnowledgeStateMachine stateMachine)
    {
        _db = db;
        _stateMachine = stateMachine;
    }

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
        const string sql = @"UPDATE ""Documents"" SET ""ProcessingStatus"" = 'Processing' WHERE ""Id"" = @id AND ""ProcessingStatus"" = 'Pending' RETURNING ""Id"", ""UserId"", ""FileName"", ""StoragePath"", ""ProcessingStatus"", ""CreatedAtUtc"", ""UpdatedAtUtc"", ""FailureReason"", ""KnowledgeStatus"", ""AIEnrichmentStatus""";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", documentId);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        var statusStr = reader.GetString(4);
        var status = Enum.Parse<ProcessingStatus>(statusStr);
        var knowledgeStr = reader.IsDBNull(8) ? "None" : reader.GetString(8);
        var knowledgeStatus = Enum.Parse<KnowledgeStatus>(knowledgeStr);
        var aiStr = reader.IsDBNull(9) ? null : reader.GetString(9);
        var aiStatus = string.IsNullOrEmpty(aiStr) ? (AIEnrichmentStatus?)null : Enum.Parse<AIEnrichmentStatus>(aiStr);
        return new Document(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetString(2),
            reader.GetString(3),
            status,
            reader.GetDateTime(5),
            reader.GetDateTime(6),
            reader.IsDBNull(7) ? null : reader.GetString(7),
            knowledgeStatus,
            aiStatus);
    }

    public async Task ResetToPendingAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var doc = await GetByIdAsync(documentId, cancellationToken).ConfigureAwait(false);
        if (doc is null) return;
        _stateMachine.TransitionToPending(doc);
        _db.Documents.Update(doc);
    }

    public async Task<IReadOnlyList<Guid>> GetStuckProcessingDocumentIdsAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default) =>
        await _db.Documents
            .AsNoTracking()
            .Where(d => d.ProcessingStatus == ProcessingStatus.Processing && d.UpdatedAtUtc < cutoffUtc)
            .Select(d => d.Id)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Guid>> GetFailedDocumentIdsAsync(CancellationToken cancellationToken = default) =>
        await _db.Documents
            .AsNoTracking()
            .Where(d => d.ProcessingStatus == ProcessingStatus.Failed)
            .Select(d => d.Id)
            .ToListAsync(cancellationToken);

    public async Task<int> ResetFailedDocumentsToPendingAsync(CancellationToken cancellationToken = default)
    {
        var ids = await GetFailedDocumentIdsAsync(cancellationToken).ConfigureAwait(false);
        foreach (var id in ids)
        {
            var doc = await GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
            if (doc is null) continue;
            try
            {
                _stateMachine.TransitionToPending(doc);
                _db.Documents.Update(doc);
            }
            catch
            {
                // Invalid transition for this document; skip
            }
        }
        return ids.Count;
    }
}
