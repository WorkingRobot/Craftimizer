namespace Craftimizer.Simulator.Actions;

internal sealed class TricksOfTheTrade : BaseAction
{
    public TricksOfTheTrade()
    {
        Category = ActionCategory.Other;
        Level = 13;
        ActionId = 100371;
        DurabilityCost = 0;
    }

    public override void CPCost(Simulator s, ref int cost)
    {
        cost = 0;
    }

    public override bool CouldUse(Simulator s, ref int cost) =>
        (s.Condition == Condition.Good || s.Condition == Condition.Excellent || s.HasEffect(EffectType.HeartAndSoul))
        && base.CouldUse(s, ref cost);

    public override void UseSuccess(Simulator s, ref int eff)
    {
        s.RestoreCP(20);
        if (s.Condition != Condition.Good && s.Condition != Condition.Excellent)
            s.RemoveEffect(EffectType.HeartAndSoul);
    }
}
