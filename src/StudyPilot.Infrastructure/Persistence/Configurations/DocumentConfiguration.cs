using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyPilot.Domain.Entities;

namespace StudyPilot.Infrastructure.Persistence.Configurations;

public sealed class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.ToTable("Documents");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();
        builder.Property(d => d.CreatedAtUtc).HasConversion(static v => v, static v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        builder.Property(d => d.UpdatedAtUtc).HasConversion(static v => v, static v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

        builder.Property(d => d.UserId);
        builder.Property(d => d.FileName).HasMaxLength(500);
        builder.Property(d => d.StoragePath).HasMaxLength(2000);
        builder.Property(d => d.ProcessingStatus).HasConversion<string>().HasMaxLength(50);

        builder.HasIndex(d => d.UserId);
        builder.HasIndex(d => d.ProcessingStatus);
    }
}
