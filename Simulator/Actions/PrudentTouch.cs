namespace Craftimizer.Simulator.Actions;

internal sealed class PrudentTouch : BaseAction
{
    public override ActionCategory Category => ActionCategory.Quality;
    public override int Level => 66;
    public override uint ActionId => 100227;

    public override bool IncreasesQuality => true;
    public override int DurabilityCost => base.DurabilityCost / 2;

    public override int CPCost(Simulator s) => 25;
    public override int Efficiency(Simulator s) => 100;

    public override bool CouldUse(Simulator s) =>
        !(s.HasEffect(EffectType.WasteNot) || s.HasEffect(EffectType.WasteNot2))
        && base.CouldUse(s);
}
