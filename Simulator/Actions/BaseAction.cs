using System.Runtime.CompilerServices;
using System.Text;

namespace Craftimizer.Simulator.Actions;

public abstract class BaseAction(
    ActionCategory category, int level, uint actionId,
    int macroWaitTime = 3,
    bool increasesProgress = false, bool increasesQuality = false,
    int durabilityCost = 10, bool increasesStepCount = true,
    int defaultCPCost = 0,
    int defaultEfficiency = 0,
    int defaultSuccessRate = 100)
{
    // Non-instanced properties

    // Metadata
    public readonly ActionCategory Category = category;

    public readonly int Level = level;
    // Doesn't matter from which class, we'll use the sheet to extrapolate the rest
    public readonly uint ActionId = actionId;
    // Seconds
    public readonly int MacroWaitTime = macroWaitTime;

    // Action properties
    public readonly bool IncreasesProgress = increasesProgress;
    public readonly bool IncreasesQuality = increasesQuality;
    public readonly int DurabilityCost = durabilityCost;
    public readonly bool IncreasesStepCount = increasesStepCount;

    // Instanced properties
    public readonly int DefaultCPCost = defaultCPCost;
    public readonly int DefaultEfficiency = defaultEfficiency;
    public readonly int DefaultSuccessRate = defaultSuccessRate; // out of 100

    // Instanced properties
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public virtual int CPCost(Simulator s) =>
        DefaultCPCost;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public virtual int Efficiency(Simulator s) =>
        DefaultEfficiency;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public virtual int SuccessRate(Simulator s) =>
        DefaultSuccessRate;

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
        if (!s.HasEffect(EffectType.TrainedPerfection))
            s.ReduceDurability(DurabilityCost);

        if (IncreasesStepCount)
        {
            if (s.Durability > 0)
                if (s.HasEffect(EffectType.Manipulation))
                    s.RestoreDurability(5);

            s.IncreaseStepCount();

            s.ActiveEffects.DecrementDuration();
        }

        s.ActionStates.MutateState(this);
        s.ActionCount++;
    }

    public virtual void UseSuccess(Simulator s)
    {
        var eff = Efficiency(s);
        if (eff != 0)
        {
            if (IncreasesProgress)
                s.IncreaseProgress(eff);
            if (IncreasesQuality)
                s.IncreaseQuality(eff);
        }
    }

    public virtual string GetTooltip(Simulator s, bool addUsability)
    {
        var cost = CPCost(s);
        var eff = Efficiency(s);
        var success = SuccessRate(s);

        var builder = new StringBuilder();
        if (addUsability && !CanUse(s))
            builder.AppendLine($"Cannot Use");
        builder.AppendLine($"Level {Level}");
        if (cost != 0)
            builder.AppendLine($"-{s.CalculateCPCost(cost)} CP");
        if (DurabilityCost != 0)
            builder.AppendLine($"-{s.CalculateDurabilityCost(DurabilityCost)} Durability");
        if (eff != 0)
        {
            if (IncreasesProgress)
                builder.AppendLine($"+{s.CalculateProgressGain(eff)} Progress");
            if (IncreasesQuality)
                builder.AppendLine($"+{s.CalculateQualityGain(eff)} Quality");
        }
        if (!IncreasesStepCount)
            builder.AppendLine($"Does Not Increase Step Count");
        if (success != 100)
            builder.AppendLine($"{s.CalculateSuccessRate(success)}% Success Rate");
        return builder.ToString();
    }
}
