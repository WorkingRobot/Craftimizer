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
    public virtual float Efficiency(Simulator s) => 0f;
    public virtual float SuccessRate(Simulator s) => 1f;

    public virtual bool CanUse(Simulator s) =>
        s.Input.Stats.Level >= Level && s.CP >= CPCost(s);

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
            builder.AppendLine($"{s.CalculateSuccessRate(SuccessRate(s)) * 100:##}%% Success Rate");
        return builder.ToString();
    }

    private static bool VerifyDurability2(int durabilityA, int durability, Effects effects)
    {
        var wasteNots = effects.HasEffect(EffectType.WasteNot) || effects.HasEffect(EffectType.WasteNot2);
        // -A
        durability -= (int)MathF.Ceiling(durabilityA * (wasteNots ? .5f : 1f));
        if (durability <= 0)
            return false;

        // If we can do the first action and still have durability left to survive to the next
        // step (even before the Manipulation modifier), we can certainly do the next action.
        return true;
    }

    public static bool VerifyDurability2(SimulationState s, int durabilityA) =>
        VerifyDurability2(durabilityA, s.Durability, s.ActiveEffects);

    public static bool VerifyDurability2(Simulator s, int durabilityA) =>
        VerifyDurability2(durabilityA, s.Durability, s.ActiveEffects);

    public static bool VerifyDurability3(int durabilityA, int durabilityB, int durability, Effects effects)
    {
        var wasteNots = Math.Max(effects.GetDuration(EffectType.WasteNot), effects.GetDuration(EffectType.WasteNot2));
        var manips = effects.HasEffect(EffectType.Manipulation);

        durability -= (int)MathF.Ceiling(durabilityA * wasteNots > 0 ? .5f : 1f);
        if (durability <= 0)
            return false;

        if (manips)
            durability += 5;

        if (wasteNots > 0)
            wasteNots--;

        durability -= (int)MathF.Ceiling(durabilityB * wasteNots > 0 ? .5f : 1f);

        if (durability <= 0)
            return false;

        // If we can do the second action and still have durability left to survive to the next
        // step (even before the Manipulation modifier), we can certainly do the next action.
        return true;
    }

    public static bool VerifyDurability3(Simulator s, int durabilityA, int durabilityB) =>
        VerifyDurability3(durabilityA, durabilityB, s.Durability, s.ActiveEffects);

    public static bool VerifyDurability3(SimulationState s, int durabilityA, int durabilityB) =>
        VerifyDurability3(durabilityA, durabilityB, s.Durability, s.ActiveEffects);
}
