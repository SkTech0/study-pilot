namespace StudyPilot.Application.Abstractions.AI;

public sealed class AIFailureClassificationResult
{
    public AIFailureKind Kind { get; init; }
    public bool AllowRetry { get; init; }
    public double RetryDelaySeconds { get; init; }
    public bool OpenCircuit { get; init; }
    public bool StopPipelineSafely { get; init; }
}
