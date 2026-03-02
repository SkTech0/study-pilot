namespace StudyPilot.Application.Common.Errors;

public static class ValidationErrorFactory
{
    public static AppError Create(string code, string message, string? field = null) =>
        new AppError(code, message, field, ErrorSeverity.Validation, null);
}
