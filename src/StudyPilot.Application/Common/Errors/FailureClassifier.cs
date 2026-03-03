using System.Net;
using System.Net.Sockets;

namespace StudyPilot.Application.Common.Errors;

public static class FailureClassifier
{
    public static FailureCategory Classify(Exception ex)
    {
        if (ex is OperationCanceledException or TaskCanceledException)
            return FailureCategory.TimeoutFailure;
        if (ex is TimeoutException)
            return FailureCategory.TimeoutFailure;
        if (ex is HttpRequestException)
            return FailureCategory.DependencyUnavailable;
        if (ex is TaskCanceledException tce && tce.InnerException is HttpRequestException)
            return FailureCategory.DependencyUnavailable;
        if (ex is FluentValidation.ValidationException)
            return FailureCategory.ValidationFailure;
        if (ex is DomainException)
            return FailureCategory.ConsistencyFailure;

        var inner = ex.InnerException;
        if (inner != null)
        {
            if (inner is TimeoutException)
                return FailureCategory.TimeoutFailure;
            if (inner is OperationCanceledException)
                return FailureCategory.TimeoutFailure;
            if (inner is HttpRequestException)
                return FailureCategory.DependencyUnavailable;
            if (inner is SocketException)
                return FailureCategory.DependencyUnavailable;
            var innerName = inner.GetType().FullName ?? "";
            if (innerName.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
                return FailureCategory.TransientFailure;
        }

        var exName = ex.GetType().FullName ?? "";
        if (exName.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
            return FailureCategory.TransientFailure;

        return FailureCategory.UnexpectedFailure;
    }
}
