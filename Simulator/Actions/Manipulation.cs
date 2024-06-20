namespace Craftimizer.Simulator.Actions;

internal sealed class Manipulation() : BaseBuffAction(
    ActionCategory.Durability, 65, 4574,
    EffectType.Manipulation, duration: 8,
    defaultCPCost: 96)
{
    public override bool IsPossible(Simulator s) =>
        s.Input.Stats.CanUseManipulation && base.IsPossible(s);

    public override void Use(Simulator s)
    {
        UseSuccess(s);

        s.ReduceCP(CPCost(s));

        s.IncreaseStepCount();

        s.ActionStates.MutateState(this);
        s.ActionCount++;
        
        s.ActiveEffects.DecrementDuration();
    }
}
