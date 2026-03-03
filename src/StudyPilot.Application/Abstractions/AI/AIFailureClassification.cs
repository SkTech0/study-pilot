namespace StudyPilot.Application.Abstractions.AI;

public enum AIFailureKind
{
    Transient = 0,
    RateLimit = 1,
    ProviderDown = 2,
    Permanent = 3,
    InvalidInput = 4
}
