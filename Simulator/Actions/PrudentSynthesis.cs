namespace Craftimizer.Simulator.Actions;

internal sealed class PrudentSynthesis : BaseAction
{

    public PrudentSynthesis()
    {
        Category = ActionCategory.Synthesis;
        Level = 88;
        ActionId = 100427;
        IncreasesProgress = true;
        DurabilityCost /= 2;
    }

    public override void CPCost(Simulator s, ref int cost)
    {
        cost = 18;
    }

    public override void Efficiency(Simulator s, ref int eff)
    {
        eff = 180;
    }

    public override bool CouldUse(Simulator s, ref int cost) =>
        !(s.HasEffect(EffectType.WasteNot) || s.HasEffect(EffectType.WasteNot2))
        && base.CouldUse(s, ref cost);
}
