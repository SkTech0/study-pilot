using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyPilot.Infrastructure.Persistence;

namespace StudyPilot.Infrastructure.Persistence.Configurations;

public sealed class KnowledgeEmbeddingJobConfiguration : IEntityTypeConfiguration<KnowledgeEmbeddingJob>
{
    public void Configure(EntityTypeBuilder<KnowledgeEmbeddingJob> builder)
    {
        builder.ToTable("KnowledgeEmbeddingJobs");
        builder.HasKey(j => j.Id);
        builder.Property(j => j.Id).ValueGeneratedNever();
        builder.Property(j => j.CorrelationId).HasMaxLength(64).IsRequired(false);
        builder.Property(j => j.Status).HasMaxLength(20);
        builder.Property(j => j.ClaimedBy).HasMaxLength(128).IsRequired(false);
        builder.Property(j => j.ErrorMessage).HasMaxLength(1000).IsRequired(false);
        builder.Property(j => j.CreatedAtUtc).HasConversion(static v => v, static v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        builder.Property(j => j.ClaimedAtUtc).HasConversion(static v => v, static v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : (DateTime?)null);
        builder.Property(j => j.NextRetryAtUtc).HasConversion(static v => v, static v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : (DateTime?)null);

        builder.HasIndex(j => j.Status);
        builder.HasIndex(j => new { j.Status, j.NextRetryAtUtc });
        builder.HasIndex(j => new { j.Status, j.CreatedAtUtc });

        builder.HasIndex(j => j.DocumentId)
            .IsUnique()
            .HasFilter(@"""Status"" IN ('Pending','Processing')");
    }
}

