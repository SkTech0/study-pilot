using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyPilot.Infrastructure.Persistence;

namespace StudyPilot.Infrastructure.Persistence.Configurations;

internal sealed class QuestionConceptLinkConfiguration : IEntityTypeConfiguration<QuestionConceptLink>
{
    public void Configure(EntityTypeBuilder<QuestionConceptLink> builder)
    {
        builder.ToTable("QuestionConceptLinks");
        builder.HasKey(l => new { l.QuestionId, l.ConceptId });
    }
}
