namespace Craftimizer.Simulator.Actions;

internal sealed class Manipulation : BaseBuffAction
{
    public override ActionCategory Category => ActionCategory.Durability;
    public override int Level => 65;
    public override uint ActionId => 4574;

    public override EffectType Effect => EffectType.Manipulation;
    public override byte Duration => 8;

    public override int CPCost(Simulator s) => 96;
    public override bool CanUse(Simulator s) => s.Input.Stats.CanUseManipulation && base.CanUse(s);

    public override void Use(Simulator s)
    {
        if (s.RollSuccess(SuccessRate(s)))
            UseSuccess(s);

        s.ReduceCP(CPCost(s));
        s.ReduceDurability(DurabilityCost);

        // same as base.Use(s), but manipulation effect never kicks in, even if manip is active before

        if (IncreasesStepCount)
            s.IncreaseStepCount();

        s.ActionStates.MutateState(this);
        s.ActionCount++;

        s.ActiveEffects.DecrementDuration();
    }
}
