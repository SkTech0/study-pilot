namespace StudyPilot.Application.Abstractions.Persistence;

public interface IChatMessageCitationRepository
{
    Task AddRangeAsync(Guid messageId, IReadOnlyList<Guid> chunkIds, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Guid>> GetChunkIdsByMessageIdAsync(Guid messageId, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> GetChunkIdsByMessageIdsAsync(IReadOnlyList<Guid> messageIds, CancellationToken cancellationToken = default);
}

