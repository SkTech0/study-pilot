using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace StudyPilot.Infrastructure.Persistence.Configurations;

public sealed class OptimizationSnapshotConfiguration : IEntityTypeConfiguration<OptimizationSnapshot>
{
    public void Configure(EntityTypeBuilder<OptimizationSnapshot> builder)
    {
        builder.ToTable("OptimizationSnapshots");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.CapturedAtUtc).HasConversion(static v => v, static v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        builder.HasIndex(x => x.CapturedAtUtc);
    }
}
