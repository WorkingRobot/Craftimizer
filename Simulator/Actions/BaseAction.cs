using System;
using System.Text;
using System.Threading;

namespace Craftimizer.Simulator.Actions;

public abstract class BaseAction
{
    internal static readonly ThreadLocal<Simulator?> TLSSimulation = new(false);
    protected static Simulator Simulation => TLSSimulation.Value ?? throw new NullReferenceException();

    public BaseAction() { }

    // Non-instanced properties
    public abstract ActionCategory Category { get; }
    public abstract int Level { get; }
    // Doesn't matter from which class, we'll use the sheet to extrapolate the rest
    public abstract uint ActionId { get; }

    // Instanced properties
    public abstract int CPCost { get; }
    public virtual float Efficiency => 0f;
    public virtual bool IncreasesProgress => false;
    public virtual bool IncreasesQuality => false;
    public virtual float SuccessRate => 1f;
    public virtual int DurabilityCost => 10;
    public virtual bool IncreasesStepCount => true;
    public virtual bool IsGuaranteedAction => SuccessRate == 1f;

    public virtual bool CanUse =>
        Simulation.Input.Stats.Level >= Level && Simulation.CP >= CPCost;

    public virtual void Use()
    {
        Simulation.ReduceCP(CPCost);
        Simulation.ReduceDurability(DurabilityCost);

        if (Simulation.HasEffect(EffectType.Manipulation))
            Simulation.RestoreDurability(5);

        if (Simulation.RollSuccess(SuccessRate))
            UseSuccess();

        if (IncreasesStepCount)
            Simulation.IncreaseStepCount();
    }

    public virtual void UseSuccess()
    {
        if (Efficiency != 0f)
        {
            if (IncreasesProgress)
                Simulation.IncreaseProgress(Efficiency);
            if (IncreasesQuality)
                Simulation.IncreaseQuality(Efficiency);
        }
    }

    public virtual string GetTooltip(bool addUsability)
    {
        var builder = new StringBuilder();
        if (addUsability && !CanUse)
            builder.AppendLine($"Cannot Use");
        builder.AppendLine($"Level {Level}");
        if (CPCost != 0)
            builder.AppendLine($"-{Simulation.CalculateCPCost(CPCost)} CP");
        if (DurabilityCost != 0)
            builder.AppendLine($"-{Simulation.CalculateDurabilityCost(DurabilityCost)} Durability");
        if (Efficiency != 0)
        {
            if (IncreasesProgress)
                builder.AppendLine($"+{Simulation.CalculateProgressGain(Efficiency)} Progress");
            if (IncreasesQuality)
                builder.AppendLine($"+{Simulation.CalculateQualityGain(Efficiency)} Quality");
        }
        if (!IncreasesStepCount)
            builder.AppendLine($"Does Not Increase Step Count");
        if (SuccessRate != 1f)
            builder.AppendLine($"{Simulation.CalculateSuccessRate(SuccessRate) * 100}%% Success Rate");
        return builder.ToString();
    }
}
