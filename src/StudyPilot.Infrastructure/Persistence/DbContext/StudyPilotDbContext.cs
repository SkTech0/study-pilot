using Microsoft.EntityFrameworkCore;
using StudyPilot.Domain.Common;
using StudyPilot.Domain.Entities;
using StudyPilot.Infrastructure.Persistence;
using RefreshTokenEntity = StudyPilot.Infrastructure.Persistence.RefreshToken;

namespace StudyPilot.Infrastructure.Persistence.DbContext;

public sealed class StudyPilotDbContext : Microsoft.EntityFrameworkCore.DbContext
{
    public StudyPilotDbContext(DbContextOptions<StudyPilotDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<Concept> Concepts => Set<Concept>();
    public DbSet<Domain.Entities.Quiz> Quizzes => Set<Domain.Entities.Quiz>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<UserAnswer> UserAnswers => Set<UserAnswer>();
    public DbSet<UserConceptProgress> UserConceptProgresses => Set<UserConceptProgress>();
    public DbSet<RefreshTokenEntity> RefreshTokens => Set<RefreshTokenEntity>();
    public DbSet<BackgroundJob> BackgroundJobs => Set<BackgroundJob>();
    internal DbSet<QuestionConceptLink> QuestionConceptLinks => Set<QuestionConceptLink>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(StudyPilotDbContext).Assembly);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Modified)
                entry.Property(nameof(BaseEntity.UpdatedAtUtc)).CurrentValue = DateTime.UtcNow;
        }
        return await base.SaveChangesAsync(cancellationToken);
    }
}
