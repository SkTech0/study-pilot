namespace StudyPilot.Application.Abstractions.Observability;

public interface ICorrelationIdAccessor
{
    string? Get();
    void Set(string? value);
}
