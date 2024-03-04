namespace Craftimizer.Simulator.Actions;

internal sealed class Manipulation : BaseBuffAction
{
    public override ActionCategory Category => ActionCategory.Durability;
    public override int Level => 65;
    public override uint ActionId => 4574;

    public override EffectType Effect => EffectType.Manipulation;
    public override byte Duration => 8;

    public override int CPCost(Simulator s) => 96;

    public override bool IsPossible(Simulator s) =>
        s.Input.Stats.CanUseManipulation && base.IsPossible(s);

    public override void Use(Simulator s)
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
