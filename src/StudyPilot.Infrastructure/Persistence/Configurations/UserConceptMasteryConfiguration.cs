using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyPilot.Domain.Entities;

namespace StudyPilot.Infrastructure.Persistence.Configurations;

public sealed class UserConceptMasteryConfiguration : IEntityTypeConfiguration<UserConceptMastery>
{
    public void Configure(EntityTypeBuilder<UserConceptMastery> builder)
    {
        builder.ToTable("UserConceptMasteries");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.CreatedAtUtc).HasConversion(static v => v, static v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        builder.Property(x => x.UpdatedAtUtc).HasConversion(static v => v, static v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

        builder.Property(x => x.UserId);
        builder.Property(x => x.ConceptId);
        builder.Property(x => x.MasteryScore);
        builder.Property(x => x.ConfidenceScore);
        builder.Property(x => x.Attempts);
        builder.Property(x => x.CorrectAnswers);
        builder.Property(x => x.LastInteractionUtc).HasConversion(static v => v, static v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.ConceptId);
        builder.HasIndex(x => new { x.UserId, x.ConceptId }).IsUnique();
        builder.HasIndex(x => new { x.UserId, x.MasteryScore });
    }
}
