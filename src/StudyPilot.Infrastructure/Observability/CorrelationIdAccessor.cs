using System.Threading;
using StudyPilot.Application.Abstractions.Observability;

namespace StudyPilot.Infrastructure.Observability;

public sealed class CorrelationIdAccessor : ICorrelationIdAccessor
{
    private static readonly AsyncLocal<string?> Current = new();

    public string? Get() => Current.Value;

    public void Set(string? value) => Current.Value = value;
}
