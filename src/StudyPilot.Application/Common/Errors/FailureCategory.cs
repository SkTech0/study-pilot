namespace StudyPilot.Application.Common.Errors;

/// <summary>Standardized failure categories for resilience and observability.</summary>
public enum FailureCategory
{
    TransientFailure,
    DependencyUnavailable,
    TimeoutFailure,
    ValidationFailure,
    ConsistencyFailure,
    UnexpectedFailure
}
