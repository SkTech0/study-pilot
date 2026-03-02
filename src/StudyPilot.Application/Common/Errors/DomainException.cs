namespace StudyPilot.Application.Common.Errors;

public sealed class DomainException : Exception
{
    public IReadOnlyList<AppError> Errors { get; }

    public DomainException(IReadOnlyList<AppError> errors) : base(errors.Count > 0 ? errors[0].Message : "Domain rule violated")
    {
        Errors = errors;
    }

    public DomainException(AppError error) : this(new[] { error }) { }
}
