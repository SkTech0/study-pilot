namespace StudyPilot.Application.Common.Errors;

public sealed class AppError
{
    public string Code { get; }
    public string Message { get; }
    public string? Field { get; }
    public ErrorSeverity Severity { get; }
    public string? CorrelationId { get; }

    public AppError(string code, string message, string? field, ErrorSeverity severity, string? correlationId = null)
    {
        Code = code;
        Message = message;
        Field = field;
        Severity = severity;
        CorrelationId = correlationId;
    }
}
