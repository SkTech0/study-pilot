namespace StudyPilot.Application.Common.Errors;

public sealed class AppError
{
    public string Code { get; }
    public string Message { get; }
    public string? Field { get; }
    public ErrorSeverity Severity { get; }
    public string? CorrelationId { get; }
    public FailureCategory Category { get; }

    public AppError(string code, string message, string? field, ErrorSeverity severity, string? correlationId = null, FailureCategory? category = null)
    {
        Code = code;
        Message = message;
        Field = field;
        Severity = severity;
        CorrelationId = correlationId;
        Category = category ?? FailureCategory.UnexpectedFailure;
    }
}
