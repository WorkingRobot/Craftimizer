namespace Craftimizer.Simulator.Actions;

internal sealed class IntensiveSynthesis : BaseAction
{
    public override ActionCategory Category => ActionCategory.Synthesis;
    public override int Level => 78;
    public override uint ActionId => 100315;

    public override bool IncreasesProgress => true;

    public override int CPCost<S>(Simulator<S> s) => 6;
    public override int Efficiency<S>(Simulator<S> s) => 400;

    public override bool CanUse<S>(Simulator<S> s) =>
        (s.Condition == Condition.Good || s.Condition == Condition.Excellent || s.HasEffect(EffectType.HeartAndSoul))
        && base.CanUse(s);

    public override void UseSuccess<S>(Simulator<S> s)
    {
        base.UseSuccess(s);
        if (s.Condition != Condition.Good && s.Condition != Condition.Excellent)
            s.RemoveEffect(EffectType.HeartAndSoul);
    }
}
