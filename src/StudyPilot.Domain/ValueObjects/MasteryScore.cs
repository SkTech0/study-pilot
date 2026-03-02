namespace StudyPilot.Domain.ValueObjects;

public sealed class MasteryScore
{
    private const int Min = 0;
    private const int Max = 100;

    public int Value { get; }

    private MasteryScore(int value) => Value = value;

    public static MasteryScore Create(int value)
    {
        var clamped = Math.Clamp(value, Min, Max);
        return new MasteryScore(clamped);
    }

    public MasteryScore Increase(int amount)
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Increase amount must be non-negative.");
        return Create(Value + amount);
    }

    public MasteryScore Decrease(int amount)
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Decrease amount must be non-negative.");
        return Create(Value - amount);
    }
}
