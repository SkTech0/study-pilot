using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyPilot.Domain.Entities;
using StudyPilot.Domain.ValueObjects;
using StudyPilot.Infrastructure.Persistence.ValueConverters;

namespace StudyPilot.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).ValueGeneratedNever();
        builder.Property(u => u.CreatedAtUtc).HasConversion(static v => v, static v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        builder.Property(u => u.UpdatedAtUtc).HasConversion(static v => v, static v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

        var emailConverter = new EmailValueConverter();
        var emailComparer = new ValueComparer<Email>(
            (a, b) => (a == null && b == null) || (a != null && b != null && a.Equals(b)),
            e => e == null ? 0 : e.GetHashCode(),
            e => e == null ? null! : Email.Create(e.Value));

        builder.Property(u => u.Email)
            .HasConversion(emailConverter)
            .HasMaxLength(320)
            .Metadata.SetValueComparer(emailComparer);
        builder.Property(u => u.PasswordHash).HasMaxLength(500);
        builder.Property(u => u.Role).HasConversion<string>().HasMaxLength(50);

        builder.HasIndex(u => u.Email).IsUnique();
    }
}
