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
    public abstract int CPCost(Simulator s);
    public virtual int Efficiency(Simulator s) => 0;
    public virtual float SuccessRate(Simulator s) => 1f;

    // Return true if it can be in the action pool now or in the future
    // e.g. if Heart and Soul is already used, it is impossible to use it again
    // or if it's a first step action and IsFirstStep is false
    public virtual bool IsPossible(Simulator s) =>
        s.Input.Stats.Level >= Level;

    // Return true if it can be used now
    // This already assumes that IsPossible returns true *at some point before*
    public virtual bool CouldUse(Simulator s) =>
        s.CP >= CPCost(s);

    public bool CanUse(Simulator s) =>
        IsPossible(s) && CouldUse(s);

    public virtual void Use(Simulator s)
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

    public virtual void UseSuccess(Simulator s)
    {
        if (Efficiency(s) != 0f)
        {
            if (IncreasesProgress)
                s.IncreaseProgress(Efficiency(s));
            if (IncreasesQuality)
                s.IncreaseQuality(Efficiency(s));
        }
    }

    public virtual string GetTooltip(Simulator s, bool addUsability)
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
            builder.AppendLine($"{s.CalculateSuccessRate(SuccessRate(s)) * 100:##}% Success Rate");
        return builder.ToString();
    }
}
