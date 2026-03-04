using Microsoft.AspNetCore.Mvc;
using StudyPilot.Application.Abstractions.Caching;
using StudyPilot.Infrastructure.AI;
using StudyPilot.Infrastructure.Hosting;
using StudyPilot.Infrastructure.Persistence.DbContext;

namespace StudyPilot.API.Controllers;

[ApiController]
[Route("health")]
public sealed class HealthController : ControllerBase
{
    private readonly IStudyPilotAIClient _aiClient;
    private readonly StudyPilotDbContext _db;
    private readonly ICacheService _cache;
    private readonly IWorkerHeartbeat _workerHeartbeat;
    private static readonly TimeSpan AIHealthCacheTtl = TimeSpan.FromSeconds(10);

    public HealthController(IStudyPilotAIClient aiClient, StudyPilotDbContext db, ICacheService cache, IWorkerHeartbeat workerHeartbeat)
    {
        _aiClient = aiClient;
        _db = db;
        _cache = cache;
        _workerHeartbeat = workerHeartbeat;
    }

    [HttpGet("live")]
    public IActionResult Live() => Ok();

    [HttpGet("startup")]
    public async Task<IActionResult> Startup(CancellationToken cancellationToken)
    {
        try
        {
            await _db.Database.CanConnectAsync(cancellationToken);
            return Ok(new { status = "Started" });
        }
        catch
        {
            return StatusCode(503, new { status = "Unhealthy", reason = "Database unreachable" });
        }
    }

    [HttpGet("ready")]
    public async Task<IActionResult> Ready(CancellationToken cancellationToken)
    {
        try
        {
            await _db.Database.CanConnectAsync(cancellationToken);
            if (!_workerHeartbeat.IsAlive)
                return StatusCode(503, new { status = "Unhealthy", reason = "Worker not alive" });
            var aiStatus = await _aiClient.CheckHealthAsync(cancellationToken);
            if (aiStatus == AIHealthStatus.Unhealthy)
                return Ok(new { status = "Degraded", reason = "AI service unavailable" });
            return Ok(new { status = "Ready" });
        }
        catch
        {
            return StatusCode(503, new { status = "Unhealthy", reason = "Database unreachable" });
        }
    }

    [HttpGet("ai")]
    public async Task<IActionResult> GetAiHealth(CancellationToken cancellationToken)
    {
        var cached = await _cache.GetAsync<string>("health:ai", cancellationToken);
        if (cached != null)
            return Ok(new { status = cached });
        var status = await _aiClient.CheckHealthAsync(cancellationToken);
        var statusString = status switch
        {
            AIHealthStatus.Healthy => "Healthy",
            AIHealthStatus.Degraded => "Degraded",
            _ => "Unhealthy"
        };
        await _cache.SetAsync("health:ai", statusString, AIHealthCacheTtl, cancellationToken);
        return Ok(new { status = statusString });
    }

    [HttpGet("detailed")]
    public async Task<IActionResult> GetDetailed(CancellationToken cancellationToken)
    {
        var dbOk = false;
        try
        {
            await _db.Database.CanConnectAsync(cancellationToken);
            dbOk = true;
        }
        catch { /* ignore */ }

        var aiStatus = AIHealthStatus.Unhealthy;
        try
        {
            aiStatus = await _aiClient.CheckHealthAsync(cancellationToken);
        }
        catch { /* ignore */ }

        return Ok(new
        {
            api = "ok",
            embedding = aiStatus != AIHealthStatus.Unhealthy ? "ok" : "fail",
            chat_provider = aiStatus switch { AIHealthStatus.Healthy => "ok", AIHealthStatus.Degraded => "degraded", _ => "fail" },
            fallback_available = true,
            db = dbOk ? "ok" : "fail"
        });
    }
}
