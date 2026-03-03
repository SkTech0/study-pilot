namespace StudyPilot.Application.Abstractions.AI;

/// <summary>
/// Classifies AI failures for decision-based retries. Used by recovery worker and outbox dispatcher.
/// </summary>
public interface IAIFailureClassifier
{
    AIFailureClassificationResult Classify(Exception exception, int currentRetryCount, int maxRetries);
}
