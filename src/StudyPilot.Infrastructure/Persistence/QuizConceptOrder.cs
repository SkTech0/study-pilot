namespace StudyPilot.Infrastructure.Persistence;

/// <summary>
/// Adaptive quiz: ordered concept IDs per quiz (50% weak, 30% medium, 20% strong).
/// </summary>
public sealed class QuizConceptOrder
{
    public Guid QuizId { get; set; }
    public int QuestionIndex { get; set; }
    public Guid ConceptId { get; set; }
}
