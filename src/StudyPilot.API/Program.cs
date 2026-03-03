using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Serilog;
using StudyPilot.API.Extensions;
using StudyPilot.API.Middleware;
using StudyPilot.API.Services;
using StudyPilot.Infrastructure;
using StudyPilot.Infrastructure.BackgroundJobs;
using StudyPilot.Infrastructure.Persistence.DbContext;
using StudyPilot.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    var maxBody = builder.Configuration.GetValue<long?>("Kestrel:MaxRequestBodySize");
    options.Limits.MaxRequestBodySize = maxBody is > 0 ? maxBody.Value : 2 * 1024 * 1024;
    options.AddServerHeader = false;
});

builder.Host.ConfigureHostOptions(o => o.ShutdownTimeout = TimeSpan.FromSeconds(30));

builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownProxies.Clear();
});

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .CreateLogger();
builder.Host.UseSerilog();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:4200")
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});
builder.Services.AddOpenApi();
builder.Services.AddCookiePolicy(options => { });
builder.Services.AddHsts(options =>
{
    options.Preload = true;
    options.IncludeSubDomains = true;
    options.MaxAge = TimeSpan.FromDays(365);
});
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApiLayer();
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddStudyPilotOpenTelemetry(builder.Configuration);
var isDevelopment = builder.Environment.IsDevelopment();
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("auth-policy", o =>
    {
        o.PermitLimit = isDevelopment ? 500 : 10;
        o.Window = TimeSpan.FromMinutes(1);
    });
    options.AddFixedWindowLimiter("upload-policy", o =>
    {
        o.PermitLimit = isDevelopment ? 30 : 5;
        o.Window = TimeSpan.FromMinutes(1);
    });
    options.AddFixedWindowLimiter("quiz-policy", o =>
    {
        o.PermitLimit = isDevelopment ? 60 : 20;
        o.Window = TimeSpan.FromMinutes(1);
    });
    options.AddFixedWindowLimiter("chat-policy", o =>
    {
        o.PermitLimit = isDevelopment ? 100 : 20;
        o.Window = TimeSpan.FromMinutes(1);
    });
    options.OnRejected = async (context, _) =>
    {
        context.HttpContext.Response.StatusCode = 429;
        await context.HttpContext.Response.WriteAsync("Too many requests.");
    };
});

StartupConfigurationValidator.Validate(builder.Configuration);

var app = builder.Build();

// Run migrations before any hosted services (workers) start, so the schema exists when they hit the DB.
try
{
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<StudyPilotDbContext>();
        await db.Database.MigrateAsync();
    }
}
catch (PostgresException pgEx) when (pgEx.SqlState == "42501")
{
    var fix = "Run as superuser: psql -U postgres -d StudyPilot -f scripts/postgres-grant-public.sql (see Prompts/RUN.md).";
    Log.Fatal("Database migration failed: permission denied. {Fix}", fix);
    throw new InvalidOperationException("Permission denied for schema public. PostgreSQL 15+ requires explicit grants and the vector extension. " + fix, pgEx);
}
catch (PostgresException pgEx) when (pgEx.SqlState == "42704" && (pgEx.MessageText?.Contains("vector") == true))
{
    var fix = "The pgvector extension is not installed. Run as superuser: psql -U postgres -d StudyPilot -f scripts/postgres-grant-public.sql";
    Log.Fatal("Database migration failed: type \"vector\" does not exist. {Fix}", fix);
    throw new InvalidOperationException("The vector type does not exist. Create the pgvector extension first. " + fix, pgEx);
}

StudyPilotMetrics.SetQueueLengthProvider(() => app.Services.GetRequiredService<IBackgroundQueueMetrics>().QueuedCount);
StudyPilotMetrics.SetQuizQueueLengthProvider(() => app.Services.GetRequiredService<DbBackedQuizQuestionGenerationJobQueue>().CachedPendingCount);
StudyPilotMetrics.SetEmbeddingQueueLengthProvider(() => app.Services.GetRequiredService<DbBackedKnowledgeEmbeddingJobQueue>().CachedPendingCount);

app.UseForwardedHeaders();
app.UseCors();
app.UseMiddleware<SecurityHeadersMiddleware>();
if (app.Environment.IsProduction())
    app.UseHsts();
app.UseCookiePolicy();
app.UseSerilogRequestLogging();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ChaosSimulationMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseApiMiddleware();
if (app.Environment.IsProduction())
    app.UseHttpsRedirection();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.MapControllers();
app.MapPrometheusScrapingEndpoint("/metrics");

app.Run();
