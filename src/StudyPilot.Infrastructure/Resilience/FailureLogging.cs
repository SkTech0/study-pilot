using System.Diagnostics;
using Microsoft.Extensions.Logging;
using StudyPilot.Application.Common.Errors;

namespace StudyPilot.Infrastructure.Resilience;

public static class FailureLogging
{
    public static void LogDependencyFailure(
        ILogger logger,
        string dependencyName,
        string operationName,
        Stopwatch elapsed,
        Exception ex,
        string? correlationId = null)
    {
        var category = FailureClassifier.Classify(ex);
        logger.LogError(ex,
            "DependencyFailure DependencyName={DependencyName} OperationName={OperationName} FailureCategory={FailureCategory} ElapsedMs={ElapsedMs} CorrelationId={CorrelationId}",
            dependencyName, operationName, category, elapsed.ElapsedMilliseconds, correlationId ?? "");
    }
}
