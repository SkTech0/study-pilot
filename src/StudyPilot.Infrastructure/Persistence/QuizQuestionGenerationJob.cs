namespace StudyPilot.Infrastructure.Persistence;

public sealed class QuizQuestionGenerationJob
{
    public Guid Id { get; set; }
    public Guid QuizId { get; set; }
    public int QuestionIndex { get; set; }
    public string? CorrelationId { get; set; }
    public string Status { get; set; } = "Pending";
    public DateTime? ClaimedAtUtc { get; set; }
    public string? ClaimedBy { get; set; }
    public int RetryCount { get; set; }
    public int MaxRetries { get; set; }
    public DateTime? NextRetryAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string? ErrorMessage { get; set; }
}
