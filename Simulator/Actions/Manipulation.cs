namespace Craftimizer.Simulator.Actions;

internal sealed class Manipulation : BaseBuffAction
{
    public Manipulation()
    {
        Category = ActionCategory.Durability;
        Level = 65;
        ActionId = 4574;
        Effect = EffectType.Manipulation;
        Duration = 8;
    }

    public override void CPCost(Simulator s, ref int cost)
    {
        cost = 96;
    }

    public override bool IsPossible(Simulator s) =>
        s.Input.Stats.CanUseManipulation && base.IsPossible(s);

    public override void Use(Simulator s, ref int cost, ref float success, ref int eff)
    {
        UseSuccess(s, ref eff);
        CPCost(s, ref cost);

        s.ReduceCP(cost);

        s.IncreaseStepCount();

        s.ActionStates.MutateState(this);
        s.ActionCount++;

        if (IncreasesStepCount)
            s.ActiveEffects.DecrementDuration();
    }
}
