namespace Craftimizer.Simulator.Actions;

internal sealed class PrudentSynthesis : BaseAction
{
    public override ActionCategory Category => ActionCategory.Synthesis;
    public override int Level => 88;
    public override uint ActionId => 100427;

    public override bool IncreasesProgress => true;
    public override int DurabilityCost => base.DurabilityCost / 2;

    public override int CPCost(Simulator s) => 18;
    public override float Efficiency(Simulator s) => 1.80f;

    public override bool CanUse(Simulator s) =>
        !(s.HasEffect(EffectType.WasteNot) || s.HasEffect(EffectType.WasteNot2))
        && base.CanUse(s);
}
