using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyPilot.Domain.Entities;

namespace StudyPilot.Infrastructure.Persistence.Configurations;

public sealed class QuestionConfiguration : IEntityTypeConfiguration<Question>
{
    public void Configure(EntityTypeBuilder<Question> builder)
    {
        builder.ToTable("Questions");
        builder.HasKey(q => q.Id);
        builder.Property(q => q.Id).ValueGeneratedNever();
        builder.Property(q => q.CreatedAtUtc).HasConversion(static v => v, static v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        builder.Property(q => q.UpdatedAtUtc).HasConversion(static v => v, static v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

        builder.Property(q => q.QuizId);
        builder.Property(q => q.QuestionIndex);
        builder.Property(q => q.Text).HasMaxLength(2000);
        builder.Property(q => q.QuestionType).HasConversion<string>().HasMaxLength(50);
        builder.Property(q => q.CorrectAnswer).HasMaxLength(2000);
        builder.Property(q => q.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(q => q.GenerationAttempts);
        builder.Property(q => q.ErrorMessage).HasMaxLength(2000);
        builder.Property(q => q.PromptVersion).HasMaxLength(100);
        builder.Property(q => q.ModelUsed).HasMaxLength(100);
        builder.Property<List<string>>("_options")
            .HasConversion(
                static v => JsonSerializer.Serialize(v),
                static v => JsonSerializer.Deserialize<List<string>>(v) ?? new List<string>())
            .HasColumnName("Options")
            .HasColumnType("jsonb");

        builder.HasIndex(q => q.QuizId);
        builder.HasIndex(q => new { q.QuizId, q.QuestionIndex }).IsUnique();
    }
}
