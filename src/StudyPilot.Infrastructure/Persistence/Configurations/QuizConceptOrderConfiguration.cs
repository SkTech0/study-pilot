using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace StudyPilot.Infrastructure.Persistence.Configurations;

public sealed class QuizConceptOrderConfiguration : IEntityTypeConfiguration<QuizConceptOrder>
{
    public void Configure(EntityTypeBuilder<QuizConceptOrder> builder)
    {
        builder.ToTable("QuizConceptOrders");
        builder.HasKey(x => new { x.QuizId, x.QuestionIndex });
        builder.Property(x => x.QuizId);
        builder.Property(x => x.QuestionIndex);
        builder.Property(x => x.ConceptId);
        builder.HasIndex(x => x.QuizId);
    }
}
