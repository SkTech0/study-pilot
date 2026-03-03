using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace StudyPilot.Infrastructure.Persistence.Configurations;

public sealed class OptimizationConfigConfiguration : IEntityTypeConfiguration<OptimizationConfig>
{
    public void Configure(EntityTypeBuilder<OptimizationConfig> builder)
    {
        builder.ToTable("OptimizationConfigs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.LastUpdatedUtc).HasConversion(static v => v, static v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
    }
}
