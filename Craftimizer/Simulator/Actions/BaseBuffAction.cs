using System;
using System.Text;

namespace Craftimizer.Simulator.Actions;

public abstract class BaseBuffAction : BaseAction
{
    public BaseBuffAction(Simulation simulation) : base(simulation) { }

    public abstract Effect Effect { get; }
    public abstract int EffectDuration { get; }
    public virtual Effect[] ConflictingEffects => Array.Empty<Effect>();

    public override int DurabilityCost => 0;

    public override void UseSuccess()
    {
        if (ConflictingEffects.Length != 0)
            foreach(var effect in ConflictingEffects)
                Simulation.RemoveEffect(effect);
        Simulation.AddEffect(Effect, EffectDuration);
    }

    public override string Tooltip {
        get
        {
            var builder = new StringBuilder(base.Tooltip);
            builder.AppendLine($"Effect: {Effect.Status().Name}");
            builder.AppendLine($"Duration: {EffectDuration} steps");
            foreach(var effect in ConflictingEffects)
                builder.AppendLine($"Conflicts with: {effect.Status().Name}");
            return builder.ToString();
        }
    }
}
