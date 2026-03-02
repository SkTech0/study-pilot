using StudyPilot.Application.Common.Errors;

namespace StudyPilot.Application.Common.Models;

public sealed class Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public IReadOnlyList<AppError> Errors { get; }

    private Result(bool isSuccess, T? value, IReadOnlyList<AppError>? errors)
    {
        IsSuccess = isSuccess;
        Value = value;
        Errors = errors ?? Array.Empty<AppError>();
    }

    public static Result<T> Success(T value) => new(true, value, null);
    public static Result<T> ValidationFailure(IReadOnlyList<AppError> errors) => new(false, default, errors);
    public static Result<T> Failure(IReadOnlyList<AppError> errors) => new(false, default, errors);
    public static Result<T> Failure(AppError error) => new(false, default, new[] { error });
}
