using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyPilot.Domain.Entities;

namespace StudyPilot.Infrastructure.Persistence.Configurations;

public sealed class UserAnswerConfiguration : IEntityTypeConfiguration<UserAnswer>
{
    public void Configure(EntityTypeBuilder<UserAnswer> builder)
    {
        builder.ToTable("UserAnswers");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.CreatedAtUtc).HasConversion(static v => v, static v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        builder.Property(a => a.UpdatedAtUtc).HasConversion(static v => v, static v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

        builder.Property(a => a.UserId);
        builder.Property(a => a.QuestionId);
        builder.Property(a => a.SubmittedAnswer).HasMaxLength(2000);
        builder.Property(a => a.IsCorrect);

        builder.HasIndex(a => a.UserId);
        builder.HasIndex(a => a.QuestionId);
    }
}
