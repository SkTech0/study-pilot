using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace StudyPilot.Infrastructure.Persistence.Configurations;

public sealed class KnowledgeTokenUsageConfiguration : IEntityTypeConfiguration<KnowledgeTokenUsage>
{
    public void Configure(EntityTypeBuilder<KnowledgeTokenUsage> builder)
    {
        builder.ToTable("KnowledgeTokenUsage");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).ValueGeneratedOnAdd();
        builder.Property(u => u.TimestampUtc).HasConversion(static v => v, static v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        builder.Property(u => u.EstimatedTokens);
        builder.HasIndex(u => u.TimestampUtc);
    }
}
