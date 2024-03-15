namespace Craftimizer.Simulator.Actions;

internal sealed class PreparatoryTouch : BaseAction
{
    public PreparatoryTouch()
    {
        Category = ActionCategory.Quality;
        Level = 71;
        ActionId = 100299;
        IncreasesQuality = true;
        DurabilityCost = 20;
    }

    public override void CPCost(Simulator s, ref int cost)
    {
        cost = 40;
    }

    public override void Efficiency(Simulator s, ref int eff)
    {
        eff = 200;
    }

    public override void UseSuccess(Simulator s, ref int eff)
    {
        base.UseSuccess(s, ref eff);
        s.StrengthenEffect(EffectType.InnerQuiet);
    }
}
