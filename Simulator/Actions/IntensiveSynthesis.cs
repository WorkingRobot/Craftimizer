namespace Craftimizer.Simulator.Actions;

internal sealed class IntensiveSynthesis : BaseAction
{
    public IntensiveSynthesis()
    {
        Category = ActionCategory.Synthesis;
        Level = 78;
        ActionId = 100315;
        IncreasesProgress = true;
    }

    public override void CPCost(Simulator s, ref int cost)
    {
        cost = 6;
    }

    public override void Efficiency(Simulator s, ref int eff)
    {
        eff = 400;
    }

    public override bool CouldUse(Simulator s, ref int cost) =>
        (s.Condition == Condition.Good || s.Condition == Condition.Excellent || s.HasEffect(EffectType.HeartAndSoul))
        && base.CouldUse(s, ref cost);

    public override void UseSuccess(Simulator s, ref int eff)
    {
        base.UseSuccess(s, ref eff);
        if (s.Condition != Condition.Good && s.Condition != Condition.Excellent)
            s.RemoveEffect(EffectType.HeartAndSoul);
    }
}
