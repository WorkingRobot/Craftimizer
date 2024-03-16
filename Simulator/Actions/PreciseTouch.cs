namespace Craftimizer.Simulator.Actions;

internal sealed class PreciseTouch() : BaseAction(
    ActionCategory.Quality, 53, 100128,
    increasesQuality: true,
    defaultCPCost: 18,
    defaultEfficiency: 150
    )
{
    public override bool CouldUse(Simulator s) =>
        (s.Condition == Condition.Good || s.Condition == Condition.Excellent || s.HasEffect(EffectType.HeartAndSoul))
        && base.CouldUse(s);

    public override void UseSuccess(Simulator s)
    {
        base.UseSuccess(s);
        s.StrengthenEffect(EffectType.InnerQuiet);
        if (s.Condition != Condition.Good && s.Condition != Condition.Excellent)
            s.RemoveEffect(EffectType.HeartAndSoul);
    }
}
