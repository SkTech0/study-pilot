using StudyPilot.Domain.Common;
using StudyPilot.Domain.Enums;

namespace StudyPilot.Domain.Entities;

public class Question : BaseEntity
{
    public Guid QuizId { get; private set; }
    public string Text { get; private set; }
    public QuestionType QuestionType { get; private set; }
    public string CorrectAnswer { get; private set; }

    private readonly List<string> _options = new();
    public IReadOnlyCollection<string> Options => _options.AsReadOnly();

    private Question() : base()
    {
        Text = null!;
        CorrectAnswer = null!;
    }

    public Question(Guid quizId, string text, QuestionType questionType, string correctAnswer, IReadOnlyList<string>? options = null) : base()
    {
        QuizId = quizId;
        Text = string.IsNullOrWhiteSpace(text)
            ? throw new ArgumentException("Question text cannot be empty.", nameof(text))
            : text;
        QuestionType = questionType;
        CorrectAnswer = correctAnswer ?? throw new ArgumentNullException(nameof(correctAnswer));
        if (options != null)
            _options.AddRange(options);
    }

    public Question(Guid id, Guid quizId, string text, QuestionType questionType, string correctAnswer, IReadOnlyList<string>? options, DateTime createdAtUtc, DateTime updatedAtUtc) : base(id, createdAtUtc, updatedAtUtc)
    {
        QuizId = quizId;
        Text = text;
        QuestionType = questionType;
        CorrectAnswer = correctAnswer;
        if (options != null)
            _options.AddRange(options);
    }
}
