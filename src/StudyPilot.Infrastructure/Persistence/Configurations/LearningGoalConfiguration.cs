using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyPilot.Domain.Entities;
using StudyPilot.Domain.Enums;

namespace StudyPilot.Infrastructure.Persistence.Configurations;

public sealed class LearningGoalConfiguration : IEntityTypeConfiguration<LearningGoal>
{
    public void Configure(EntityTypeBuilder<LearningGoal> builder)
    {
        builder.ToTable("LearningGoals");
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id).ValueGeneratedNever();
        builder.Property(g => g.CreatedAtUtc).HasConversion(static v => v, static v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        builder.Property(g => g.UpdatedAtUtc).HasConversion(static v => v, static v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

        builder.Property(g => g.UserId);
        builder.Property(g => g.TutorSessionId);
        builder.Property(g => g.ConceptId);
        builder.Property(g => g.GoalType).HasConversion<string>().HasMaxLength(20);
        builder.Property(g => g.Priority);
        builder.Property(g => g.ProgressPercent);
        builder.Property(g => g.CreatedUtc).HasConversion(static v => v, static v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

        builder.HasIndex(g => g.TutorSessionId);
        builder.HasIndex(g => g.UserId);
        builder.HasIndex(g => new { g.UserId, g.Priority });
    }
}
