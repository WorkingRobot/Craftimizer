namespace Craftimizer.Simulator.Actions;

internal sealed class PreciseTouch : BaseAction
{
    public PreciseTouch()
    {
        Category = ActionCategory.Quality;
        Level = 53;
        ActionId = 100128;
        IncreasesQuality = true;
    }

    public override void CPCost(Simulator s, ref int cost)
    {
        cost = 18;
    }

    public override void Efficiency(Simulator s, ref int eff)
    {
        eff = 150;
    }

    public override bool CouldUse(Simulator s, ref int cost) =>
        (s.Condition == Condition.Good || s.Condition == Condition.Excellent || s.HasEffect(EffectType.HeartAndSoul))
        && base.CouldUse(s, ref cost);

    public override void UseSuccess(Simulator s, ref int eff)
    {
        base.UseSuccess(s, ref eff);
        s.StrengthenEffect(EffectType.InnerQuiet);
        if (s.Condition != Condition.Good && s.Condition != Condition.Excellent)
            s.RemoveEffect(EffectType.HeartAndSoul);
    }
}
