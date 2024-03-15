using System.Text;

namespace Craftimizer.Simulator.Actions;

public abstract class BaseAction
{
    // Non-instanced properties

    // Metadata
    public ActionCategory Category;

    public int Level;
    // Doesn't matter from which class, we'll use the sheet to extrapolate the rest
    public uint ActionId;
    // Seconds
    public int MacroWaitTime = 3;

    // Action properties
    public bool IncreasesProgress;
    public bool IncreasesQuality;
    public int DurabilityCost = 10;
    public bool IncreasesStepCount = true;
    public int EfficiencyFactor;
    public float SuccessRateFactor = 1;

    // Instanced properties
    public abstract void CPCost(Simulator s, ref int cost);

    public virtual void Efficiency(Simulator s, ref int eff)
    {
        eff = EfficiencyFactor;
    }

    public virtual void SuccessRate(Simulator s, ref float success)
    {
        success = SuccessRateFactor;
    }

    // Return true if it can be in the action pool now or in the future
    // e.g. if Heart and Soul is already used, it is impossible to use it again
    // or if it's a first step action and IsFirstStep is false
    public virtual bool IsPossible(Simulator s) =>
        s.Input.Stats.Level >= Level;

    // Return true if it can be used now
    // This already assumes that IsPossible returns true *at some point before*
    public virtual bool CouldUse(Simulator s, ref int cost)
    {
        CPCost(s, ref cost);
        return s.CP >= cost;
    }

    public bool CanUse(Simulator s, ref int cost) =>
        IsPossible(s) && CouldUse(s, ref cost);

    public virtual void Use(Simulator s, ref int cost, ref float success, ref int eff)
    {
        SuccessRate(s, ref success);
        if (s.RollSuccess(success))
            UseSuccess(s, ref eff);
        CPCost(s, ref cost);

        s.ReduceCP(cost);
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

    public virtual void UseSuccess(Simulator s, ref int eff)
    {
        Efficiency(s, ref eff);
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
        var builder = new StringBuilder();
        int cost = 0;
        float success = 1f;
        if (addUsability && !CanUse(s, ref cost))
            builder.AppendLine($"Cannot Use");
        builder.AppendLine($"Level {Level}");
        if (cost != 0)
            builder.AppendLine($"-{s.CalculateCPCost(cost)} CP");
        if (DurabilityCost != 0)
            builder.AppendLine($"-{s.CalculateDurabilityCost(DurabilityCost)} Durability");
        Efficiency(s, ref cost);
        if (cost != 0)
        {
            if (IncreasesProgress)
                builder.AppendLine($"+{s.CalculateProgressGain(cost)} Progress");
            if (IncreasesQuality)
                builder.AppendLine($"+{s.CalculateQualityGain(cost)} Quality");
        }
        if (!IncreasesStepCount)
            builder.AppendLine($"Does Not Increase Step Count");
        SuccessRate(s, ref success);
        if (Math.Abs(success - 1f) > float.Epsilon)
            builder.AppendLine($"{s.CalculateSuccessRate(success) * 100:##}% Success Rate");
        return builder.ToString();
    }
}
