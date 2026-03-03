using Microsoft.EntityFrameworkCore;
using StudyPilot.Domain.Common;
using StudyPilot.Domain.Entities;
using StudyPilot.Infrastructure.Persistence;
using RefreshTokenEntity = StudyPilot.Infrastructure.Persistence.RefreshToken;
using QuizConceptOrderEntity = StudyPilot.Infrastructure.Persistence.QuizConceptOrder;

namespace StudyPilot.Infrastructure.Persistence.DbContext;

public sealed class StudyPilotDbContext : Microsoft.EntityFrameworkCore.DbContext
{
    public StudyPilotDbContext(DbContextOptions<StudyPilotDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<Concept> Concepts => Set<Concept>();
    public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();
    public DbSet<ChatSession> ChatSessions => Set<ChatSession>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<Domain.Entities.Quiz> Quizzes => Set<Domain.Entities.Quiz>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<UserAnswer> UserAnswers => Set<UserAnswer>();
    public DbSet<UserConceptProgress> UserConceptProgresses => Set<UserConceptProgress>();
    public DbSet<UserConceptMastery> UserConceptMasteries => Set<UserConceptMastery>();
    public DbSet<LearningInsight> LearningInsights => Set<LearningInsight>();
    public DbSet<QuizConceptOrderEntity> QuizConceptOrders => Set<QuizConceptOrderEntity>();
    public DbSet<TutorSession> TutorSessions => Set<TutorSession>();
    public DbSet<LearningGoal> LearningGoals => Set<LearningGoal>();
    public DbSet<TutorExercise> TutorExercises => Set<TutorExercise>();
    public DbSet<TutorMessage> TutorMessages => Set<TutorMessage>();
    public DbSet<RefreshTokenEntity> RefreshTokens => Set<RefreshTokenEntity>();
    public DbSet<BackgroundJob> BackgroundJobs => Set<BackgroundJob>();
    public DbSet<QuizQuestionGenerationJob> QuizQuestionGenerationJobs => Set<QuizQuestionGenerationJob>();
    public DbSet<KnowledgeEmbeddingJob> KnowledgeEmbeddingJobs => Set<KnowledgeEmbeddingJob>();
    internal DbSet<QuestionConceptLink> QuestionConceptLinks => Set<QuestionConceptLink>();
    internal DbSet<ChatMessageCitation> ChatMessageCitations => Set<ChatMessageCitation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");
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
