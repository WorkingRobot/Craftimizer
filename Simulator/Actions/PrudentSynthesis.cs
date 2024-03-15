namespace Craftimizer.Simulator.Actions;

internal sealed class PrudentSynthesis : BaseAction
{
    public override ActionCategory Category => ActionCategory.Synthesis;
    public override int Level => 88;
    public override uint ActionId => 100427;

    public override bool IncreasesProgress => true;
    public override int DurabilityCost => base.DurabilityCost / 2;

    public override int CPCost(Simulator s) => 18;
    public override int Efficiency(Simulator s) => 180;

    public override bool CouldUse(Simulator s) =>
        !(s.HasEffect(EffectType.WasteNot) || s.HasEffect(EffectType.WasteNot2))
        && base.CouldUse(s);
}
