using System;
using System.Text;

namespace Craftimizer.Simulator.Actions;

public abstract class BaseBuffAction : BaseAction
{
    public BaseBuffAction(Simulation simulation) : base(simulation) { }

    public abstract Effect Effect { get; }
    public virtual EffectType[] ConflictingEffects => Array.Empty<EffectType>();

    public override int DurabilityCost => 0;

    public override void UseSuccess()
    {
        if (ConflictingEffects.Length != 0)
            foreach(var effect in ConflictingEffects)
                Simulation.RemoveEffect(effect);
        Simulation.AddEffect(Effect.Type, Effect.Duration, Effect.Strength);
    }

    public override string GetTooltip(bool addUsability)
    {
        var builder = new StringBuilder(base.GetTooltip(addUsability));
        builder.AppendLine($"Effect: {Effect.Tooltip}");
        return builder.ToString();
    }
}
