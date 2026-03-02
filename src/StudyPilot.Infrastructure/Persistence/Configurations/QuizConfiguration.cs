using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace StudyPilot.Infrastructure.Persistence.Configurations;

public sealed class QuizConfiguration : IEntityTypeConfiguration<Domain.Entities.Quiz>
{
    public void Configure(EntityTypeBuilder<Domain.Entities.Quiz> builder)
    {
        builder.ToTable("Quizzes");
        builder.HasKey(q => q.Id);
        builder.Property(q => q.Id).ValueGeneratedNever();
        builder.Property(q => q.CreatedAtUtc).HasConversion(static v => v, static v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        builder.Property(q => q.UpdatedAtUtc).HasConversion(static v => v, static v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

        builder.Property(q => q.DocumentId);
        builder.Property(q => q.CreatedForUserId);
        builder.Property(q => q.TotalQuestionCount);

        builder.HasIndex(q => q.DocumentId);
        var questionRelationship = builder.HasMany(typeof(Domain.Entities.Question))
            .WithOne()
            .HasForeignKey(nameof(Domain.Entities.Question.QuizId))
            .IsRequired();
        questionRelationship.Metadata.PrincipalToDependent?.SetField("_questions");
    }
}
