using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyPilot.Domain.Entities;
using StudyPilot.Domain.ValueObjects;
using StudyPilot.Infrastructure.Persistence.ValueConverters;

namespace StudyPilot.Infrastructure.Persistence.Configurations;

public sealed class UserConceptProgressConfiguration : IEntityTypeConfiguration<UserConceptProgress>
{
    public void Configure(EntityTypeBuilder<UserConceptProgress> builder)
    {
        builder.ToTable("UserConceptProgresses");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.CreatedAtUtc).HasConversion(static v => v, static v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        builder.Property(p => p.UpdatedAtUtc).HasConversion(static v => v, static v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

        builder.Property(p => p.UserId);
        builder.Property(p => p.ConceptId);
        var masteryConverter = new MasteryScoreValueConverter();
        var masteryComparer = new ValueComparer<MasteryScore>(
            (a, b) => (a == null && b == null) || (a != null && b != null && a.Value == b.Value),
            e => e == null ? 0 : e.Value.GetHashCode(),
            e => e == null ? null! : MasteryScore.Create(e.Value));
        builder.Property(p => p.MasteryScore)
            .HasConversion(masteryConverter)
            .Metadata.SetValueComparer(masteryComparer);
        builder.Property(p => p.Attempts);
        builder.Property("_correctCount").HasColumnName("CorrectCount");
        builder.Property(p => p.LastReviewedUtc).HasConversion(static v => v, static v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

        builder.HasIndex(p => p.UserId);
        builder.HasIndex(p => p.ConceptId);
        builder.HasIndex(p => new { p.UserId, p.ConceptId });
    }
}
