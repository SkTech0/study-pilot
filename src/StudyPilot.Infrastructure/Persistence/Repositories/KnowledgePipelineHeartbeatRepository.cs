using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using StudyPilot.Infrastructure.Persistence;
using StudyPilot.Infrastructure.Persistence.DbContext;

namespace StudyPilot.Infrastructure.Persistence.Repositories;

public sealed class KnowledgePipelineHeartbeatRepository : IKnowledgePipelineHeartbeatRepository
{
    private readonly StudyPilotDbContext _db;

    public KnowledgePipelineHeartbeatRepository(StudyPilotDbContext db) => _db = db;

    public async Task UpsertAsync(KnowledgePipelineHeartbeat heartbeat, CancellationToken cancellationToken = default)
    {
        var conn = (NpgsqlConnection)_db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            await conn.OpenAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(@"
INSERT INTO ""KnowledgePipelineHeartbeats"" (""InstanceId"", ""LastSeenUtc"", ""CurrentMode"", ""OutboxPending"", ""EmbeddingDepth"", ""AILimiterWaiters"")
VALUES (@id, @seen, @mode, @outbox, @embed, @waiters)
ON CONFLICT (""InstanceId"") DO UPDATE SET
  ""LastSeenUtc"" = EXCLUDED.""LastSeenUtc"",
  ""CurrentMode"" = EXCLUDED.""CurrentMode"",
  ""OutboxPending"" = EXCLUDED.""OutboxPending"",
  ""EmbeddingDepth"" = EXCLUDED.""EmbeddingDepth"",
  ""AILimiterWaiters"" = EXCLUDED.""AILimiterWaiters""", conn);
        cmd.Parameters.AddWithValue("id", heartbeat.InstanceId);
        cmd.Parameters.AddWithValue("seen", heartbeat.LastSeenUtc);
        cmd.Parameters.AddWithValue("mode", heartbeat.CurrentMode);
        cmd.Parameters.AddWithValue("outbox", heartbeat.OutboxPending);
        cmd.Parameters.AddWithValue("embed", heartbeat.EmbeddingDepth);
        cmd.Parameters.AddWithValue("waiters", heartbeat.AILimiterWaiters);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<KnowledgePipelineHeartbeat>> GetActiveHeartbeatsAsync(TimeSpan withinLast, CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow - withinLast;
        return await _db.KnowledgePipelineHeartbeats
            .AsNoTracking()
            .Where(h => h.LastSeenUtc >= cutoff)
            .ToListAsync(cancellationToken);
    }
}
