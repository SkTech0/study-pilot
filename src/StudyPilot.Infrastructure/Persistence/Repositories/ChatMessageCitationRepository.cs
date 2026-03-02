using Microsoft.EntityFrameworkCore;
using StudyPilot.Application.Abstractions.Persistence;
using StudyPilot.Infrastructure.Persistence.DbContext;

namespace StudyPilot.Infrastructure.Persistence.Repositories;

public sealed class ChatMessageCitationRepository : IChatMessageCitationRepository
{
    private readonly StudyPilotDbContext _db;

    public ChatMessageCitationRepository(StudyPilotDbContext db) => _db = db;

    public async Task AddRangeAsync(Guid messageId, IReadOnlyList<Guid> chunkIds, CancellationToken cancellationToken = default)
    {
        if (chunkIds.Count == 0) return;
        var entities = chunkIds
            .Distinct()
            .Select(id => new Persistence.ChatMessageCitation { MessageId = messageId, ChunkId = id })
            .ToList();
        await _db.ChatMessageCitations.AddRangeAsync(entities, cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> GetChunkIdsByMessageIdAsync(Guid messageId, CancellationToken cancellationToken = default) =>
        await _db.ChatMessageCitations.AsNoTracking()
            .Where(x => x.MessageId == messageId)
            .Select(x => x.ChunkId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> GetChunkIdsByMessageIdsAsync(IReadOnlyList<Guid> messageIds, CancellationToken cancellationToken = default)
    {
        if (messageIds.Count == 0)
            return new Dictionary<Guid, IReadOnlyList<Guid>>();

        var rows = await _db.ChatMessageCitations.AsNoTracking()
            .Where(x => messageIds.Contains(x.MessageId))
            .Select(x => new { x.MessageId, x.ChunkId })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(x => x.MessageId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<Guid>)g.Select(x => x.ChunkId).Distinct().ToList());
    }
}

