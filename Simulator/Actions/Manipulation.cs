namespace Craftimizer.Simulator.Actions;

internal sealed class Manipulation : BaseBuffAction
{
    public override ActionCategory Category => ActionCategory.Durability;
    public override int Level => 65;
    public override uint ActionId => 4574;

    public override EffectType Effect => EffectType.Manipulation;
    public override byte Duration => 8;

    public override int CPCost<S>(Simulator<S> s) => 96;
    public override bool CanUse<S>(Simulator<S> s) => s.Input.Stats.CanUseManipulation && base.CanUse(s);

    public override void Use<S>(Simulator<S> s)
    {
        UseSuccess(s);

        s.ReduceCP(CPCost(s));

        s.IncreaseStepCount();

        s.ActionStates.MutateState(this);
        s.ActionCount++;

        if (IncreasesStepCount)
            s.ActiveEffects.DecrementDuration();
    }
}
