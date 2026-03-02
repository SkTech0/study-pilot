namespace StudyPilot.Infrastructure.Persistence;

internal sealed class QuestionConceptLink
{
    public Guid QuestionId { get; set; }
    public Guid ConceptId { get; set; }
}
