namespace Craftimizer.Simulator.Actions;

internal sealed class PreciseTouch : BaseAction
{
    public override ActionCategory Category => ActionCategory.Quality;
    public override int Level => 53;
    public override uint ActionId => 100128;

    public override bool IncreasesQuality => true;

    public override int CPCost(Simulator s) => 18;
    public override int Efficiency(Simulator s) => 150;

    public override bool CanUse(Simulator s) =>
        (s.Condition == Condition.Good || s.Condition == Condition.Excellent || s.HasEffect(EffectType.HeartAndSoul))
        && base.CanUse(s);

    public override void UseSuccess(Simulator s)
    {
        base.UseSuccess(s);
        s.StrengthenEffect(EffectType.InnerQuiet);
        if (s.Condition != Condition.Good && s.Condition != Condition.Excellent)
            s.RemoveEffect(EffectType.HeartAndSoul);
    }
}
