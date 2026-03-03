using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyPilot.Domain.Entities;
using StudyPilot.Domain.Enums;

namespace StudyPilot.Infrastructure.Persistence.Configurations;

public sealed class TutorSessionConfiguration : IEntityTypeConfiguration<TutorSession>
{
    public void Configure(EntityTypeBuilder<TutorSession> builder)
    {
        builder.ToTable("TutorSessions");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.CreatedAtUtc).HasConversion(static v => v, static v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        builder.Property(s => s.UpdatedAtUtc).HasConversion(static v => v, static v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

        builder.Property(s => s.UserId);
        builder.Property(s => s.DocumentId);
        builder.Property(s => s.SessionState).HasConversion<string>().HasMaxLength(20);
        builder.Property(s => s.CurrentStep).HasConversion<string>().HasMaxLength(20);
        builder.Property(s => s.CurrentGoalId);
        builder.Property(s => s.StartedUtc).HasConversion(static v => v, static v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        builder.Property(s => s.LastInteractionUtc).HasConversion(static v => v, static v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

        builder.HasIndex(s => s.UserId);
        builder.HasIndex(s => new { s.UserId, s.SessionState });
    }
}
