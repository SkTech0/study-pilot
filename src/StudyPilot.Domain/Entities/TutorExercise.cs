using StudyPilot.Domain.Common;

namespace StudyPilot.Domain.Entities;

public sealed class TutorExercise : BaseEntity
{
    public Guid TutorSessionId { get; private set; }
    public Guid ConceptId { get; private set; }
    public string Question { get; private set; }
    public string ExpectedAnswer { get; private set; }
    public string Difficulty { get; private set; }
    public DateTime CreatedUtc { get; private set; }

    public TutorExercise(Guid tutorSessionId, Guid conceptId, string question, string expectedAnswer, string difficulty = "medium") : base()
    {
        TutorSessionId = tutorSessionId;
        ConceptId = conceptId;
        Question = question ?? "";
        ExpectedAnswer = expectedAnswer ?? "";
        Difficulty = string.IsNullOrWhiteSpace(difficulty) ? "medium" : difficulty.Trim();
        CreatedUtc = DateTime.UtcNow;
    }

    internal TutorExercise(Guid id, Guid tutorSessionId, Guid conceptId, string question, string expectedAnswer,
        string difficulty, DateTime createdUtc, DateTime createdAtUtc, DateTime updatedAtUtc)
        : base(id, createdAtUtc, updatedAtUtc)
    {
        TutorSessionId = tutorSessionId;
        ConceptId = conceptId;
        Question = question;
        ExpectedAnswer = expectedAnswer;
        Difficulty = difficulty;
        CreatedUtc = createdUtc;
    }
}
