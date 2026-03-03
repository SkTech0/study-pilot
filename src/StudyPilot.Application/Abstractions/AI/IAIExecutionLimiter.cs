namespace StudyPilot.Application.Abstractions.AI;

/// <summary>
/// Centralized backpressure for all AI calls. Prevents worker overload and protects external providers.
/// All AI calls (concept extraction, embedding, etc.) must acquire before and release after.
/// </summary>
public interface IAIExecutionLimiter
{
    /// <summary>Wait for capacity (respects max concurrent AI calls and circuit state).</summary>
    Task WaitForCapacityAsync(CancellationToken cancellationToken = default);

    /// <summary>Release a slot after the AI call completes (success or failure).</summary>
    void Release();

    /// <summary>Current number of in-flight AI operations (for metrics).</summary>
    int CurrentConcurrency { get; }

    /// <summary>Whether the limiter is allowing new work (circuit not open).</summary>
    bool IsAvailable { get; }

    /// <summary>Number of callers currently waiting for capacity (for load metrics).</summary>
    int WaitersCount { get; }

    /// <summary>Open or close circuit (e.g. after PROVIDER_DOWN classification). When open, WaitForCapacityAsync throws.</summary>
    void SetCircuitOpen(bool open);

    /// <summary>Current circuit state: Closed, Open, HalfOpen. Observable via metrics.</summary>
    CircuitState CircuitState { get; }

    /// <summary>Call after successful AI call when in HalfOpen to transition back to Closed.</summary>
    void NotifySuccess();
}
