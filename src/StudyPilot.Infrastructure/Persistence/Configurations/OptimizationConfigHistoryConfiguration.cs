using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace StudyPilot.Infrastructure.Persistence.Configurations;

public sealed class OptimizationConfigHistoryConfiguration : IEntityTypeConfiguration<OptimizationConfigHistory>
{
    public void Configure(EntityTypeBuilder<OptimizationConfigHistory> builder)
    {
        builder.ToTable("OptimizationConfigHistory");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.AppliedAtUtc).HasConversion(static v => v, static v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        builder.HasIndex(x => x.AppliedAtUtc);
    }
}
