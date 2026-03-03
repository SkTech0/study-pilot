using System.Data;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using StudyPilot.Application.Abstractions.Knowledge;
using StudyPilot.Application.Abstractions.Persistence;
using StudyPilot.Application.Knowledge.Models;
using StudyPilot.Infrastructure.Persistence.DbContext;
using StudyPilot.Infrastructure.Services;

namespace StudyPilot.Infrastructure.Knowledge;

public sealed class HybridSearchService : IHybridSearchService
{
    private const int VectorTopK = 24;
    private const int KeywordTopK = 24;
    private const int FinalTopK = 12;
    private const double VectorWeight = 0.6;
    private const double KeywordWeight = 0.2;
    private const double MasteryWeight = 0.2;

    private static readonly string KeywordSqlWithDoc = @"
SELECT ""Id"" AS ""ChunkId"", ""DocumentId"", ""ChunkText"" AS ""Text"", ""TokenCount"",
       ts_rank_cd(""SearchVector"", query) AS ""Score""
FROM ""DocumentChunks"", plainto_tsquery('english', @q) AS query
WHERE ""UserId"" = @userId AND ""DocumentId"" = @documentId AND ""SearchVector"" @@ query
ORDER BY ""Score"" DESC NULLS LAST
LIMIT @k";

    private static readonly string KeywordSqlGlobal = @"
SELECT ""Id"" AS ""ChunkId"", ""DocumentId"", ""ChunkText"" AS ""Text"", ""TokenCount"",
       ts_rank_cd(""SearchVector"", query) AS ""Score""
FROM ""DocumentChunks"", plainto_tsquery('english', @q) AS query
WHERE ""UserId"" = @userId AND ""SearchVector"" @@ query
ORDER BY ""Score"" DESC NULLS LAST
LIMIT @k";

    private readonly StudyPilotDbContext _db;
    private readonly IVectorSearchService _vectorSearch;
    private readonly IUserConceptMasteryRepository _masteryRepository;
    private readonly IConceptRepository _conceptRepository;
    private readonly ILogger<HybridSearchService> _logger;

    public HybridSearchService(
        StudyPilotDbContext db,
        IVectorSearchService vectorSearch,
        IUserConceptMasteryRepository masteryRepository,
        IConceptRepository conceptRepository,
        ILogger<HybridSearchService> logger)
    {
        _db = db;
        _vectorSearch = vectorSearch;
        _masteryRepository = masteryRepository;
        _conceptRepository = conceptRepository;
        _logger = logger;
    }

    public async Task<IReadOnlyList<RetrievedChunk>> SearchAsync(
        Guid userId,
        float[] queryEmbedding,
        Guid? documentId,
        string queryText,
        int topK,
        CancellationToken cancellationToken = default)
    {
        var swTotal = Stopwatch.StartNew();
        var vectorSw = Stopwatch.StartNew();
        var vectorResults = await _vectorSearch.SearchAsync(userId, queryEmbedding, documentId, VectorTopK, cancellationToken).ConfigureAwait(false);
        vectorSw.Stop();
        StudyPilotMetrics.VectorSearchMs.Record(vectorSw.ElapsedMilliseconds);

        IReadOnlyList<RetrievedChunk> keywordResults = Array.Empty<RetrievedChunk>();
        var keywordOk = false;
        if (!string.IsNullOrWhiteSpace(queryText) && queryText.Length <= 500)
        {
            try
            {
                var kwSw = Stopwatch.StartNew();
                keywordResults = await RunKeywordSearchAsync(userId, documentId, queryText.Trim(), cancellationToken).ConfigureAwait(false);
                kwSw.Stop();
                StudyPilotMetrics.HybridRerankMs.Record(kwSw.ElapsedMilliseconds);
                keywordOk = true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Keyword search failed; using vector-only. Query length: {Length}", queryText.Length);
            }
        }

        double masteryBoost = 0.5;
        if (documentId.HasValue)
        {
            var concepts = await _conceptRepository.GetByDocumentIdAsync(documentId.Value, cancellationToken).ConfigureAwait(false);
            if (concepts.Count > 0)
            {
                var conceptIds = concepts.Select(c => c.Id).ToList();
                var masteries = await _masteryRepository.GetByUserAndConceptsAsync(userId, conceptIds, cancellationToken).ConfigureAwait(false);
                if (masteries.Count > 0)
                {
                    var avg = masteries.Average(m => m.MasteryScore);
                    masteryBoost = Math.Clamp(1.0 - (avg / 100.0), 0, 1);
                    StudyPilotMetrics.PersonalizationBoostApplied.Add(1);
                }
            }
        }

        if (!keywordOk || keywordResults.Count == 0)
            return vectorResults.Take(topK).ToList();

        var rerankSw = Stopwatch.StartNew();
        var merged = MergeAndRerank(vectorResults, keywordResults, masteryBoost);
        rerankSw.Stop();
        StudyPilotMetrics.HybridRerankMs.Record(rerankSw.ElapsedMilliseconds);

        return merged.Take(topK).ToList();
    }

    private async Task<IReadOnlyList<RetrievedChunk>> RunKeywordSearchAsync(
        Guid userId,
        Guid? documentId,
        string queryText,
        CancellationToken cancellationToken)
    {
        var conn = (NpgsqlConnection)_db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        var sql = documentId.HasValue ? KeywordSqlWithDoc : KeywordSqlGlobal;
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("q", queryText);
        cmd.Parameters.AddWithValue("userId", userId);
        cmd.Parameters.AddWithValue("k", KeywordTopK);
        if (documentId.HasValue)
            cmd.Parameters.AddWithValue("documentId", documentId.Value);

        var rows = new List<RetrievedChunk>();
        await using (var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var score = reader.IsDBNull(4) ? 0.0 : reader.GetDouble(4);
                rows.Add(new RetrievedChunk(
                    reader.GetGuid(0),
                    reader.GetGuid(1),
                    reader.GetString(2),
                    reader.GetInt32(3),
                    score));
            }
        }
        return rows;
    }

    private static List<RetrievedChunk> MergeAndRerank(
        IReadOnlyList<RetrievedChunk> vectorResults,
        IReadOnlyList<RetrievedChunk> keywordResults,
        double masteryBoost)
    {
        // finalScore = vectorScore*0.6 + keywordScore*0.2 + masteryBoost*0.2 (prioritize weak areas)
        var keywordMax = keywordResults.Count > 0 ? Math.Max(keywordResults.Max(c => c.Score), 1e-6) : 1.0;
        var byId = new Dictionary<Guid, (RetrievedChunk Chunk, double VectorSim, double KeywordNorm)>();

        foreach (var c in vectorResults)
        {
            var sim = Math.Max(0, 1.0 - c.Score);
            byId[c.ChunkId] = (c, sim, 0);
        }
        foreach (var c in keywordResults)
        {
            var norm = c.Score / keywordMax;
            if (byId.TryGetValue(c.ChunkId, out var existing))
                byId[c.ChunkId] = (existing.Chunk, existing.VectorSim, norm);
            else
                byId[c.ChunkId] = (c, 0, norm);
        }

        return byId.Values
            .Select(x => (x.Chunk, FinalScore: x.VectorSim * VectorWeight + x.KeywordNorm * KeywordWeight + masteryBoost * MasteryWeight))
            .OrderByDescending(x => x.FinalScore)
            .Select(x => x.Chunk)
            .Take(FinalTopK)
            .ToList();
    }
}
