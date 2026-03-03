using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Options;
using StudyPilot.Application.Abstractions.AI;
using StudyPilot.Application.Abstractions.Optimization;
using StudyPilot.Infrastructure.Services;

namespace StudyPilot.Infrastructure.AI;

public sealed class AIExecutionLimiter : IAIExecutionLimiter
{
    private const int CooldownBaseSeconds = 30;
    private const int CooldownMaxSeconds = 300;

    private readonly SemaphoreSlim _slotAvailable = new(0);
    private readonly SemaphoreSlim _halfOpenGate = new(1, 1);
    private readonly AIServiceOptions _options;
    private readonly IOptimizationConfigProvider _configProvider;
    private int _currentCount;
    private int _waitersCount;
    private int _circuitStateInt = (int)CircuitState.Closed;
    private DateTime _openUntilUtc;
    private int _consecutiveFailures;
    private readonly object _gate = new();
    private readonly Meter _meter;
    private readonly ObservableGauge<int> _circuitStateGauge;

    public AIExecutionLimiter(IOptions<AIServiceOptions> options, IOptimizationConfigProvider configProvider)
    {
        _options = options.Value;
        _configProvider = configProvider;
        _meter = new Meter(StudyPilotMetrics.MeterName, "1.0");
        _circuitStateGauge = _meter.CreateObservableGauge("knowledge_circuit_state", () => (int)CircuitState);
    }

    public int CurrentConcurrency => Volatile.Read(ref _currentCount);
    public int WaitersCount => Volatile.Read(ref _waitersCount);
    public bool IsAvailable => CircuitState != CircuitState.Open;

    public CircuitState CircuitState => (CircuitState)Volatile.Read(ref _circuitStateInt);

    public async Task WaitForCapacityAsync(CancellationToken cancellationToken = default)
    {
        var state = CircuitState;
        if (state == CircuitState.Open)
        {
            var now = DateTime.UtcNow;
            lock (_gate)
            {
                if ((CircuitState)Volatile.Read(ref _circuitStateInt) == CircuitState.Open && now >= _openUntilUtc)
                {
                    Volatile.Write(ref _circuitStateInt, (int)CircuitState.HalfOpen);
                }
            }
        }

        state = CircuitState;
        if (state == CircuitState.Open)
            throw new InvalidOperationException("AI execution circuit is open; retry later.");

        Interlocked.Increment(ref _waitersCount);
        try
        {
            if (state == CircuitState.HalfOpen)
            {
                await _halfOpenGate.WaitAsync(cancellationToken).ConfigureAwait(false);
                Interlocked.Increment(ref _currentCount);
            }
            else
            {
                while (true)
                {
                    var max = Math.Max(1, _configProvider.GetMaxAIConcurrency());
                    if (Interlocked.Increment(ref _currentCount) <= max)
                        break;
                    Interlocked.Decrement(ref _currentCount);
                    await _slotAvailable.WaitAsync(cancellationToken).ConfigureAwait(false);
                }
            }
            Interlocked.Decrement(ref _waitersCount);
        }
        catch
        {
            Interlocked.Decrement(ref _waitersCount);
            throw;
        }
    }

    public void Release()
    {
        var state = CircuitState;
        if (state == CircuitState.HalfOpen)
        {
            Interlocked.Decrement(ref _currentCount);
            _halfOpenGate.Release();
        }
        else
        {
            Interlocked.Decrement(ref _currentCount);
            _slotAvailable.Release(1);
        }
    }

    /// <summary>Call after successful AI call in HalfOpen to close the circuit.</summary>
    public void NotifySuccess()
    {
        lock (_gate)
        {
            if ((CircuitState)Volatile.Read(ref _circuitStateInt) == CircuitState.HalfOpen)
            {
                Volatile.Write(ref _circuitStateInt, (int)CircuitState.Closed);
                _consecutiveFailures = 0;
            }
        }
    }

    public void SetCircuitOpen(bool open)
    {
        lock (_gate)
        {
            if (open)
            {
                Volatile.Write(ref _circuitStateInt, (int)CircuitState.Open);
                _consecutiveFailures++;
                var cooldownSeconds = Math.Min(CooldownBaseSeconds * (int)Math.Pow(2, Math.Min(_consecutiveFailures - 1, 4)), CooldownMaxSeconds);
                _openUntilUtc = DateTime.UtcNow.AddSeconds(cooldownSeconds);
            }
            else
            {
                Volatile.Write(ref _circuitStateInt, (int)CircuitState.Closed);
                _consecutiveFailures = 0;
            }
        }
    }
}
