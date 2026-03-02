using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pgvector;
using StudyPilot.Application.Abstractions.Knowledge;
using StudyPilot.Application.Knowledge.Models;
using StudyPilot.Infrastructure.Persistence.DbContext;

namespace StudyPilot.Infrastructure.Knowledge;

public sealed class PgVectorSearchService : IVectorSearchService
{
    private const string SqlWithDoc = @"
SELECT ""Id"" AS ""ChunkId"", ""DocumentId"", ""ChunkText"" AS ""Text"", ""TokenCount"", (""Embedding"" <=> @q) AS ""Score""
FROM ""DocumentChunks""
WHERE ""UserId"" = @userId AND ""DocumentId"" = @documentId
ORDER BY ""Embedding"" <=> @q
LIMIT @k";
    private const string SqlGlobal = @"
SELECT ""Id"" AS ""ChunkId"", ""DocumentId"", ""ChunkText"" AS ""Text"", ""TokenCount"", (""Embedding"" <=> @q) AS ""Score""
FROM ""DocumentChunks""
WHERE ""UserId"" = @userId
ORDER BY ""Embedding"" <=> @q
LIMIT @k";

    private readonly StudyPilotDbContext _db;

    public PgVectorSearchService(StudyPilotDbContext db) => _db = db;

    public async Task<IReadOnlyList<RetrievedChunk>> SearchAsync(
        Guid userId,
        float[] queryEmbedding,
        Guid? documentId,
        int topK,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty) return Array.Empty<RetrievedChunk>();
        if (queryEmbedding is null || queryEmbedding.Length != Domain.Entities.DocumentChunk.EmbeddingDimensions)
            return Array.Empty<RetrievedChunk>();

        var k = Math.Clamp(topK, 1, 50);
        var q = new Vector(queryEmbedding);

        var conn = (NpgsqlConnection)_db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            await conn.OpenAsync(cancellationToken);

        await using var cmd = new NpgsqlCommand(documentId.HasValue ? SqlWithDoc : SqlGlobal, conn);
        cmd.Parameters.AddWithValue("q", q);
        cmd.Parameters.AddWithValue("userId", userId);
        cmd.Parameters.AddWithValue("k", k);
        if (documentId.HasValue)
            cmd.Parameters.AddWithValue("documentId", documentId.Value);

        var rows = new List<RetrievedChunk>();
        await using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(new RetrievedChunk(
                    reader.GetGuid(0),
                    reader.GetGuid(1),
                    reader.GetString(2),
                    reader.GetInt32(3),
                    reader.GetDouble(4)));
            }
        }
        return rows;
    }
}

