using StudyPilot.Application.Common.Errors;

namespace StudyPilot.API.Contracts;

public sealed class ApiResponse<T>
{
    public bool Success { get; init; }
    public T? Data { get; init; }
    public IReadOnlyList<AppError> Errors { get; init; } = [];
    public string? CorrelationId { get; init; }

    public static ApiResponse<T> Ok(T data, string? correlationId = null) =>
        new() { Success = true, Data = data, CorrelationId = correlationId };

    public static ApiResponse<T> Fail(IReadOnlyList<AppError> errors, string? correlationId = null) =>
        new() { Success = false, Errors = errors, CorrelationId = correlationId };
}
