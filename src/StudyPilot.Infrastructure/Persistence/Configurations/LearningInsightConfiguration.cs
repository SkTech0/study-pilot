using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyPilot.Domain.Entities;
using StudyPilot.Domain.Enums;

namespace StudyPilot.Infrastructure.Persistence.Configurations;

public sealed class LearningInsightConfiguration : IEntityTypeConfiguration<LearningInsight>
{
    public void Configure(EntityTypeBuilder<LearningInsight> builder)
    {
        builder.ToTable("LearningInsights");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.CreatedAtUtc).HasConversion(static v => v, static v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        builder.Property(x => x.UpdatedAtUtc).HasConversion(static v => v, static v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

        builder.Property(x => x.UserId);
        builder.Property(x => x.ConceptId);
        builder.Property(x => x.InsightType).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Source).HasConversion<string>().HasMaxLength(16);
        builder.Property(x => x.Confidence);
        builder.Property(x => x.CreatedUtc).HasConversion(static v => v, static v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.ConceptId);
        builder.HasIndex(x => new { x.UserId, x.ConceptId });
    }
}
