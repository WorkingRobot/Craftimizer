namespace Craftimizer.Simulator.Actions;

internal sealed class TrainedFinesse : BaseAction
{
    public TrainedFinesse()
    {
        Category = ActionCategory.Quality;
        Level = 90;
        ActionId = 100435;
        IncreasesQuality = true;
        DurabilityCost = 0;
    }

    public override void CPCost(Simulator s, ref int cost)
    {
        cost = 32;
    }

    public override void Efficiency(Simulator s, ref int eff)
    {
        eff = 100;
    }

    public override bool CouldUse(Simulator s, ref int cost) =>
        s.GetEffectStrength(EffectType.InnerQuiet) == 10
        && base.CouldUse(s, ref cost);
}
