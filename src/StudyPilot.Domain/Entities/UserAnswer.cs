using StudyPilot.Domain.Common;

namespace StudyPilot.Domain.Entities;

public class UserAnswer : BaseEntity
{
    public Guid UserId { get; private set; }
    public Guid QuestionId { get; private set; }
    public string SubmittedAnswer { get; private set; }
    public bool IsCorrect { get; private set; }

    public UserAnswer(Guid userId, Guid questionId, string submittedAnswer, bool isCorrect) : base()
    {
        UserId = userId;
        QuestionId = questionId;
        SubmittedAnswer = submittedAnswer ?? throw new ArgumentNullException(nameof(submittedAnswer));
        IsCorrect = isCorrect;
    }

    public UserAnswer(Guid id, Guid userId, Guid questionId, string submittedAnswer, bool isCorrect, DateTime createdAtUtc, DateTime updatedAtUtc) : base(id, createdAtUtc, updatedAtUtc)
    {
        UserId = userId;
        QuestionId = questionId;
        SubmittedAnswer = submittedAnswer;
        IsCorrect = isCorrect;
    }
}
