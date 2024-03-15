namespace Craftimizer.Simulator.Actions;

internal sealed class PrudentTouch : BaseAction
{
    public PrudentTouch()
    {
        Category = ActionCategory.Quality;
        Level = 66;
        ActionId = 100227;
        IncreasesQuality = true;
        DurabilityCost /= 2;
    }

    public override void CPCost(Simulator s, ref int cost)
    {
        cost = 25;
    }

    public override void Efficiency(Simulator s, ref int eff)
    {
        eff = 100;
    }

    public override bool CouldUse(Simulator s, ref int cost) =>
        !(s.HasEffect(EffectType.WasteNot) || s.HasEffect(EffectType.WasteNot2))
        && base.CouldUse(s, ref cost);
}
