using StudyPilot.Domain.Common;

namespace StudyPilot.Domain.Entities;

public class Quiz : BaseEntity
{
    public Guid DocumentId { get; private set; }
    public Guid CreatedForUserId { get; private set; }

    private readonly List<Question> _questions = new();
    public IReadOnlyCollection<Question> Questions => _questions.AsReadOnly();

    public Quiz(Guid documentId, Guid createdForUserId) : base()
    {
        DocumentId = documentId;
        CreatedForUserId = createdForUserId;
    }

    public Quiz(Guid id, Guid documentId, Guid createdForUserId, DateTime createdAtUtc, DateTime updatedAtUtc) : base(id, createdAtUtc, updatedAtUtc)
    {
        DocumentId = documentId;
        CreatedForUserId = createdForUserId;
    }

    public void AddQuestion(Question question)
    {
        if (question is null)
            throw new ArgumentNullException(nameof(question));
        if (question.QuizId != Id)
            throw new InvalidOperationException("Question must belong to this quiz.");
        _questions.Add(question);
        Touch();
    }
}
