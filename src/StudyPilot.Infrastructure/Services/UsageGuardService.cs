using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StudyPilot.Application.Abstractions.Persistence;
using StudyPilot.Application.Abstractions.UsageGuard;
using StudyPilot.Infrastructure.Persistence.DbContext;

namespace StudyPilot.Infrastructure.Services;

public sealed class UsageGuardService : IUsageGuardService
{
    private readonly StudyPilotDbContext _db;
    private readonly UsageGuardOptions _options;

    public UsageGuardService(StudyPilotDbContext db, IOptions<UsageGuardOptions> options)
    {
        _db = db;
        _options = options.Value;
    }

    public async Task<bool> CanUploadDocumentAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var since = DateTime.UtcNow.Date;
        var count = await _db.Documents
            .Where(d => d.UserId == userId && d.CreatedAtUtc >= since)
            .CountAsync(cancellationToken);
        return count < _options.MaxDocumentsPerUserPerDay;
    }

    public async Task<bool> CanGenerateQuizAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var since = DateTime.UtcNow.AddHours(-1);
        var count = await _db.Quizzes
            .Where(q => q.CreatedForUserId == userId && q.CreatedAtUtc >= since)
            .CountAsync(cancellationToken);
        return count < _options.MaxQuizGenerationPerHour;
    }
}
