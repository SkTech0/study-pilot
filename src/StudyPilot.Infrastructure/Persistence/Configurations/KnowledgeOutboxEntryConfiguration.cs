using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace StudyPilot.Infrastructure.Persistence.Configurations;

public sealed class KnowledgeOutboxEntryConfiguration : IEntityTypeConfiguration<KnowledgeOutboxEntry>
{
    public void Configure(EntityTypeBuilder<KnowledgeOutboxEntry> builder)
    {
        builder.ToTable("KnowledgeOutbox");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.AggregateId).IsRequired();
        builder.Property(x => x.EventType).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Payload).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(50).IsRequired();
        builder.Property(x => x.RetryCount).IsRequired();
        builder.Property(x => x.NextAttemptUtc).IsRequired(false);
        builder.Property(x => x.CreatedUtc).IsRequired();

        builder.HasIndex(x => new { x.Status, x.NextAttemptUtc });
        builder.HasIndex(x => x.AggregateId);
    }
}

