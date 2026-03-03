using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyPilot.Domain.Entities;

namespace StudyPilot.Infrastructure.Persistence.Configurations;

public sealed class TutorExerciseConfiguration : IEntityTypeConfiguration<TutorExercise>
{
    public void Configure(EntityTypeBuilder<TutorExercise> builder)
    {
        builder.ToTable("TutorExercises");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.CreatedAtUtc).HasConversion(static v => v, static v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        builder.Property(e => e.UpdatedAtUtc).HasConversion(static v => v, static v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

        builder.Property(e => e.TutorSessionId);
        builder.Property(e => e.ConceptId);
        builder.Property(e => e.Question).HasMaxLength(2000);
        builder.Property(e => e.ExpectedAnswer).HasMaxLength(2000);
        builder.Property(e => e.Difficulty).HasMaxLength(20);
        builder.Property(e => e.CreatedUtc).HasConversion(static v => v, static v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

        builder.HasIndex(e => e.TutorSessionId);
    }
}
