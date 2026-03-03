namespace StudyPilot.Infrastructure.Resilience;

/// <summary>Config-driven toggles for testing resilience. Do NOT enable in production.</summary>
public sealed class ChaosSimulationOptions
{
    public const string SectionName = "Resilience:Chaos";

    public bool SimulateAIUnavailable { get; set; }
    public bool SimulateSlowAI { get; set; }
    public int SimulateSlowAIDelayMs { get; set; } = 5000;
    public bool SimulateDBDelay { get; set; }
    public int SimulateDBDelayMs { get; set; } = 2000;
}
