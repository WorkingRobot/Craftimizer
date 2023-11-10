using System.Text;

namespace Craftimizer.Simulator.Actions;

public abstract class BaseAction
{
    // Non-instanced properties

    // Metadata
    public abstract ActionCategory Category { get; }
    public abstract int Level { get; }
    // Doesn't matter from which class, we'll use the sheet to extrapolate the rest
    public abstract uint ActionId { get; }
    // Seconds
    public virtual int MacroWaitTime => 3;

    // Action properties
    public virtual bool IncreasesProgress => false;
    public virtual bool IncreasesQuality => false;
    public virtual int DurabilityCost => 10;
    public virtual bool IncreasesStepCount => true;

    // Instanced properties
    public abstract int CPCost<S>(Simulator<S> s) where S : ISimulator;
    public virtual int Efficiency<S>(Simulator<S> s) where S : ISimulator => 0;
    public virtual float SuccessRate<S>(Simulator<S> s) where S : ISimulator => 1f;

    public virtual bool CanUse<S>(Simulator<S> s) where S : ISimulator =>
        s.Input.Stats.Level >= Level && s.CP >= CPCost(s);

    public virtual void Use<S>(Simulator<S> s) where S : ISimulator
    {
        if (s.RollSuccess(SuccessRate(s)))
            UseSuccess(s);

        s.ReduceCP(CPCost(s));
        s.ReduceDurability(DurabilityCost);

        if (s.Durability > 0)
        {
            if (s.HasEffect(EffectType.Manipulation))
                s.RestoreDurability(5);
        }

        if (IncreasesStepCount)
            s.IncreaseStepCount();

        s.ActionStates.MutateState(this);
        s.ActionCount++;

        if (IncreasesStepCount)
            s.ActiveEffects.DecrementDuration();
    }

    public virtual void UseSuccess<S>(Simulator<S> s) where S : ISimulator
    {
        if (Efficiency(s) != 0f)
        {
            if (IncreasesProgress)
                s.IncreaseProgress(Efficiency(s));
            if (IncreasesQuality)
                s.IncreaseQuality(Efficiency(s));
        }
    }

    public virtual string GetTooltip<S>(Simulator<S> s, bool addUsability) where S : ISimulator
    {
        var builder = new StringBuilder();
        if (addUsability && !CanUse(s))
            builder.AppendLine($"Cannot Use");
        builder.AppendLine($"Level {Level}");
        if (CPCost(s) != 0)
            builder.AppendLine($"-{s.CalculateCPCost(CPCost(s))} CP");
        if (DurabilityCost != 0)
            builder.AppendLine($"-{s.CalculateDurabilityCost(DurabilityCost)} Durability");
        if (Efficiency(s) != 0)
        {
            if (IncreasesProgress)
                builder.AppendLine($"+{s.CalculateProgressGain(Efficiency(s))} Progress");
            if (IncreasesQuality)
                builder.AppendLine($"+{s.CalculateQualityGain(Efficiency(s))} Quality");
        }
        if (!IncreasesStepCount)
            builder.AppendLine($"Does Not Increase Step Count");
        if (SuccessRate(s) != 1f)
            builder.AppendLine($"{s.CalculateSuccessRate(SuccessRate(s)) * 100:##}%% Success Rate");
        return builder.ToString();
    }
}
