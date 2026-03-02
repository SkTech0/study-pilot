using StudyPilot.Domain.Common;

namespace StudyPilot.Domain.Entities;

public sealed class DocumentChunk : BaseEntity
{
    public const int EmbeddingDimensions = 1536;

    public Guid DocumentId { get; private set; }
    public Guid UserId { get; private set; }
    public string ChunkText { get; private set; }
    public int TokenCount { get; private set; }
    public float[] Embedding { get; private set; }

    public DocumentChunk(Guid documentId, Guid userId, string chunkText, int tokenCount, float[] embedding) : base()
    {
        if (documentId == Guid.Empty) throw new ArgumentException("DocumentId cannot be empty.", nameof(documentId));
        if (userId == Guid.Empty) throw new ArgumentException("UserId cannot be empty.", nameof(userId));
        ChunkText = string.IsNullOrWhiteSpace(chunkText) ? throw new ArgumentException("Chunk text cannot be empty.", nameof(chunkText)) : chunkText;
        if (tokenCount < 0) throw new ArgumentOutOfRangeException(nameof(tokenCount), "TokenCount cannot be negative.");
        if (embedding is null) throw new ArgumentNullException(nameof(embedding));
        if (embedding.Length != EmbeddingDimensions) throw new ArgumentException($"Embedding must be length {EmbeddingDimensions}.", nameof(embedding));

        DocumentId = documentId;
        UserId = userId;
        TokenCount = tokenCount;
        Embedding = embedding;
    }

    public DocumentChunk(Guid id, Guid documentId, Guid userId, string chunkText, int tokenCount, float[] embedding, DateTime createdAtUtc, DateTime updatedAtUtc) : base(id, createdAtUtc, updatedAtUtc)
    {
        DocumentId = documentId;
        UserId = userId;
        ChunkText = chunkText;
        TokenCount = tokenCount;
        Embedding = embedding;
    }
}

