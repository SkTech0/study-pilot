using System.Data;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using StudyPilot.Application.Abstractions.Knowledge;
using StudyPilot.Application.Abstractions.Optimization;
using StudyPilot.Application.Abstractions.Persistence;
using StudyPilot.Application.Knowledge.Constants;
using StudyPilot.Application.Knowledge.Models;
using StudyPilot.Infrastructure.Optimization;
using StudyPilot.Infrastructure.Persistence.DbContext;
using StudyPilot.Infrastructure.Services;

namespace StudyPilot.Infrastructure.Knowledge;

public sealed class HybridSearchService : IHybridSearchService
{
    private const int KeywordTopK = 24;
    private const int FinalTopK = 12;
    private const double VectorWeight = 0.6;
    private const double KeywordWeight = 0.2;
    private const double MasteryWeight = 0.2;

    private static readonly string KeywordSqlWithDoc = @"
SELECT c.""Id"" AS ""ChunkId"", c.""DocumentId"", c.""ChunkText"" AS ""Text"", c.""TokenCount"",
       ts_rank_cd(c.""SearchVector"", query) AS ""Score""
FROM ""DocumentChunks"" c, plainto_tsquery('english', @q) AS query
INNER JOIN ""Documents"" d ON d.""Id"" = c.""DocumentId""
WHERE c.""UserId"" = @userId AND c.""DocumentId"" = @documentId AND d.""KnowledgeStatus"" = 'Ready' AND c.""SearchVector"" @@ query
ORDER BY ""Score"" DESC NULLS LAST
LIMIT @k";

    private static readonly string KeywordSqlGlobal = @"
SELECT c.""Id"" AS ""ChunkId"", c.""DocumentId"", c.""ChunkText"" AS ""Text"", c.""TokenCount"",
       ts_rank_cd(c.""SearchVector"", query) AS ""Score""
FROM ""DocumentChunks"" c, plainto_tsquery('english', @q) AS query
INNER JOIN ""Documents"" d ON d.""Id"" = c.""DocumentId""
WHERE c.""UserId"" = @userId AND d.""KnowledgeStatus"" = 'Ready' AND c.""SearchVector"" @@ query
ORDER BY ""Score"" DESC NULLS LAST
LIMIT @k";

    private readonly StudyPilotDbContext _db;
    private readonly IVectorSearchService _vectorSearch;
    private readonly IUserConceptMasteryRepository _masteryRepository;
    private readonly IConceptRepository _conceptRepository;
    private readonly IOptimizationConfigProvider _configProvider;
    private readonly OptimizationMetricsBuffer? _metricsBuffer;
    private readonly ILogger<HybridSearchService> _logger;

    public HybridSearchService(
        StudyPilotDbContext db,
        IVectorSearchService vectorSearch,
        IUserConceptMasteryRepository masteryRepository,
        IConceptRepository conceptRepository,
        IOptimizationConfigProvider configProvider,
        ILogger<HybridSearchService> logger,
        OptimizationMetricsBuffer? metricsBuffer = null)
    {
        _db = db;
        _vectorSearch = vectorSearch;
        _masteryRepository = masteryRepository;
        _conceptRepository = conceptRepository;
        _configProvider = configProvider;
        _metricsBuffer = metricsBuffer;
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
        var vectorTopK = Math.Clamp(_configProvider.GetVectorTopK(), 6, 50);
        var swTotal = Stopwatch.StartNew();
        var vectorTask = _vectorSearch.SearchAsync(userId, queryEmbedding, documentId, vectorTopK, cancellationToken);
        var keywordTask = !string.IsNullOrWhiteSpace(queryText) && queryText.Length <= 500
            ? RunKeywordSearchAsync(userId, documentId, queryText.Trim(), cancellationToken)
            : Task.FromResult<IReadOnlyList<RetrievedChunk>>(Array.Empty<RetrievedChunk>());

        var vectorResults = await vectorTask.ConfigureAwait(false);
        StudyPilotMetrics.VectorSearchMs.Record(swTotal.ElapsedMilliseconds);

        IReadOnlyList<RetrievedChunk> keywordResults = Array.Empty<RetrievedChunk>();
        var keywordOk = false;
        try
        {
            keywordResults = await keywordTask.ConfigureAwait(false);
            keywordOk = !string.IsNullOrWhiteSpace(queryText) && queryText.Length <= 500;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Keyword search failed; using vector-only. Query length: {Length}", queryText?.Length ?? 0);
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
        {
            var result = vectorResults.Take(topK).ToList();
            _metricsBuffer?.RecordRetrieval(result.Count >= RetrievalConstants.MinimumChunksForAnswer);
            return result;
        }

        var rerankSw = Stopwatch.StartNew();
        var merged = MergeAndRerank(vectorResults, keywordResults, masteryBoost);
        rerankSw.Stop();
        StudyPilotMetrics.HybridRerankMs.Record(rerankSw.ElapsedMilliseconds);

        var final = merged.Take(topK).ToList();
        _metricsBuffer?.RecordRetrieval(final.Count >= RetrievalConstants.MinimumChunksForAnswer);
        return final;
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
