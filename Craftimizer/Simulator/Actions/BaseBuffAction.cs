using System;

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
}
