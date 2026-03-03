using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyPilot.Domain.Entities;

namespace StudyPilot.Infrastructure.Persistence.Configurations;

public sealed class TutorMessageConfiguration : IEntityTypeConfiguration<TutorMessage>
{
    public void Configure(EntityTypeBuilder<TutorMessage> builder)
    {
        builder.ToTable("TutorMessages");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.CreatedAtUtc).HasConversion(static v => v, static v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        builder.Property(m => m.UpdatedAtUtc).HasConversion(static v => v, static v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

        builder.Property(m => m.TutorSessionId);
        builder.Property(m => m.Role).HasMaxLength(20);
        builder.Property(m => m.Content).HasMaxLength(12000);
        builder.Property(m => m.CreatedUtc).HasConversion(static v => v, static v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

        builder.HasIndex(m => m.TutorSessionId);
    }
}
