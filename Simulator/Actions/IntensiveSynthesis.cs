namespace Craftimizer.Simulator.Actions;

internal sealed class IntensiveSynthesis : BaseAction
{
    public override ActionCategory Category => ActionCategory.Synthesis;
    public override int Level => 78;
    public override uint ActionId => 100315;

    public override bool IncreasesProgress => true;

    public override int CPCost(Simulator s) => 6;
    public override int Efficiency(Simulator s) => 400;

    public override bool CouldUse(Simulator s) =>
        (s.Condition == Condition.Good || s.Condition == Condition.Excellent || s.HasEffect(EffectType.HeartAndSoul))
        && base.CouldUse(s);

    public override void UseSuccess(Simulator s)
    {
        base.UseSuccess(s);
        if (s.Condition != Condition.Good && s.Condition != Condition.Excellent)
            s.RemoveEffect(EffectType.HeartAndSoul);
    }
}
