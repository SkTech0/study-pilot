using StudyPilot.Domain.Common;
using StudyPilot.Domain.Enums;

namespace StudyPilot.Domain.Entities;

public class Question : BaseEntity
{
    public Guid QuizId { get; private set; }
    public int QuestionIndex { get; private set; }
    public string Text { get; private set; }
    public QuestionType QuestionType { get; private set; }
    public string CorrectAnswer { get; private set; }
    public QuestionGenerationStatus Status { get; private set; }
    public int GenerationAttempts { get; private set; }
    public string? ErrorMessage { get; private set; }
    public string? PromptVersion { get; private set; }
    public string? ModelUsed { get; private set; }

    private readonly List<string> _options = new();
    public IReadOnlyCollection<string> Options => _options.AsReadOnly();

    private Question() : base()
    {
        Text = null!;
        CorrectAnswer = null!;
    }

    /// <summary>Creates a placeholder row for lazy generation (Status = Generating).</summary>
    public static Question CreatePlaceholder(Guid quizId, int questionIndex)
    {
        var q = new Question();
        q.SetPlaceholder(quizId, questionIndex);
        return q;
    }

    private void SetPlaceholder(Guid quizId, int questionIndex)
    {
        QuizId = quizId;
        QuestionIndex = questionIndex;
        Text = "";
        QuestionType = QuestionType.MCQ;
        CorrectAnswer = "";
        Status = QuestionGenerationStatus.Generating;
        GenerationAttempts = 0;
    }

    public Question(Guid quizId, string text, QuestionType questionType, string correctAnswer, IReadOnlyList<string>? options = null, int questionIndex = 0) : base()
    {
        QuizId = quizId;
        QuestionIndex = questionIndex;
        Text = string.IsNullOrWhiteSpace(text)
            ? throw new ArgumentException("Question text cannot be empty.", nameof(text))
            : text;
        QuestionType = questionType;
        CorrectAnswer = correctAnswer ?? throw new ArgumentNullException(nameof(correctAnswer));
        Status = QuestionGenerationStatus.Ready;
        GenerationAttempts = 1;
        if (options != null)
            _options.AddRange(options);
    }

    public Question(Guid id, Guid quizId, string text, QuestionType questionType, string correctAnswer, IReadOnlyList<string>? options, DateTime createdAtUtc, DateTime updatedAtUtc) : base(id, createdAtUtc, updatedAtUtc)
    {
        QuizId = quizId;
        QuestionIndex = 0;
        Text = text;
        QuestionType = questionType;
        CorrectAnswer = correctAnswer;
        Status = QuestionGenerationStatus.Ready;
        GenerationAttempts = 1;
        if (options != null)
            _options.AddRange(options);
    }

    public void MarkReady(string text, QuestionType questionType, string correctAnswer, IReadOnlyList<string> options, string? promptVersion = null, string? modelUsed = null)
    {
        if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Question text cannot be empty.", nameof(text));
        Text = text;
        QuestionType = questionType;
        CorrectAnswer = correctAnswer ?? "";
        _options.Clear();
        _options.AddRange(options ?? Array.Empty<string>());
        Status = QuestionGenerationStatus.Ready;
        ErrorMessage = null;
        PromptVersion = promptVersion;
        ModelUsed = modelUsed;
        Touch();
    }

    public void MarkFailed(string? errorMessage)
    {
        Status = QuestionGenerationStatus.Failed;
        ErrorMessage = errorMessage;
        Touch();
    }

    public void IncrementAttempts()
    {
        GenerationAttempts++;
        Touch();
    }
}
