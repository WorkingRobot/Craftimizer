namespace Craftimizer.Simulator.Actions;

internal sealed class TricksOfTheTrade : BaseAction
{
    public override ActionCategory Category => ActionCategory.Other;
    public override int Level => 13;
    public override uint ActionId => 100371;

    public override int DurabilityCost => 0;

    public override int CPCost<S>(Simulator<S> s) => 0;

    public override bool CanUse<S>(Simulator<S> s) =>
        (s.Condition == Condition.Good || s.Condition == Condition.Excellent || s.HasEffect(EffectType.HeartAndSoul))
        && base.CanUse(s);

    public override void UseSuccess<S>(Simulator<S> s)
    {
        s.RestoreCP(20);
        if (s.Condition != Condition.Good && s.Condition != Condition.Excellent)
            s.RemoveEffect(EffectType.HeartAndSoul);
    }
}
