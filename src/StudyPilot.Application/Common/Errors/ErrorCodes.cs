namespace StudyPilot.Application.Common.Errors;

public static class ErrorCodes
{
    public const string AuthInvalidCredentials = "AUTH_INVALID_CREDENTIALS";
    public const string AuthUserExists = "AUTH_USER_EXISTS";
    public const string DocumentTooLarge = "DOCUMENT_TOO_LARGE";
    public const string DocumentInvalidFormat = "DOCUMENT_INVALID_FORMAT";
    public const string QuizNotFound = "QUIZ_NOT_FOUND";
    public const string QuizAlreadyCompleted = "QUIZ_ALREADY_COMPLETED";
    public const string AiServiceUnavailable = "AI_SERVICE_UNAVAILABLE";
    public const string RateLimitExceeded = "RATE_LIMIT_EXCEEDED";
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string UnexpectedError = "UNEXPECTED_ERROR";
    public const string ValidationEmailInvalid = "VALIDATION_EMAIL_INVALID";
    public const string ValidationPasswordInvalid = "VALIDATION_PASSWORD_INVALID";
    public const string ValidationRequired = "VALIDATION_REQUIRED";
    public const string AuthInvalidToken = "AUTH_INVALID_TOKEN";
    public const string DocumentUploadLimitReached = "DOCUMENT_UPLOAD_LIMIT_REACHED";
    public const string DocumentNotFound = "DOCUMENT_NOT_FOUND";
    public const string DocumentNoConcepts = "DOCUMENT_NO_CONCEPTS";
    public const string QuizGenerationLimitReached = "QUIZ_GENERATION_LIMIT_REACHED";
    public const string UserNotFound = "USER_NOT_FOUND";
    public const string RefreshTokenInvalid = "REFRESH_TOKEN_INVALID";
}
