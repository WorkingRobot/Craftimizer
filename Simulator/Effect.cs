namespace Craftimizer.Simulator;

public readonly record struct Effect
{
    public EffectType Type { get; init; }
    public int? Duration { get; init; }
    public int? Strength { get; init; }

    public bool HasDuration => Duration != null;
    public bool HasStrength => Strength != null;

    public Effect DecrementDuration() => this with { Duration = Duration - 1 };
    public Effect IncrementStrength() => this with { Strength = Strength + 1 };
}
