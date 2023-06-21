namespace Craftimizer.Simulator.Actions;

internal sealed class PrudentTouch : BaseAction
{
    public override ActionCategory Category => ActionCategory.Quality;
    public override int Level => 66;
    public override uint ActionId => 100227;

    public override bool IncreasesQuality => true;
    public override int DurabilityCost => base.DurabilityCost / 2;

    public override int CPCost(Simulator s) => 25;
    public override float Efficiency(Simulator s) => 1.00f;

    public override bool CanUse(Simulator s) =>
        !(s.HasEffect(EffectType.WasteNot) || s.HasEffect(EffectType.WasteNot2))
        && base.CanUse(s);
}
