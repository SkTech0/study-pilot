using StudyPilot.Domain.Entities;

namespace StudyPilot.Application.Abstractions.Persistence;

public interface IDocumentRepository
{
    Task<Document?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Document>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(Document document, CancellationToken cancellationToken = default);
    Task UpdateAsync(Document document, CancellationToken cancellationToken = default);
    Task<Document?> TryClaimForProcessingAsync(Guid documentId, CancellationToken cancellationToken = default);
    /// <summary>Resets document to Pending so it can be re-claimed after a failed job retry.</summary>
    Task ResetToPendingAsync(Guid documentId, CancellationToken cancellationToken = default);
    /// <summary>Document IDs stuck in Processing since before cutoff (for recovery).</summary>
    Task<IReadOnlyList<Guid>> GetStuckProcessingDocumentIdsAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default);
    /// <summary>Document IDs in Failed state (for reset via state machine).</summary>
    Task<IReadOnlyList<Guid>> GetFailedDocumentIdsAsync(CancellationToken cancellationToken = default);
    /// <summary>Reset all documents in Failed state to Pending via state machine. Returns count reset. Caller must SaveChanges after if using UnitOfWork.</summary>
    Task<int> ResetFailedDocumentsToPendingAsync(CancellationToken cancellationToken = default);
}
