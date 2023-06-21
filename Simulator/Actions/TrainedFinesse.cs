namespace Craftimizer.Simulator.Actions;

internal sealed class TrainedFinesse : BaseAction
{
    public override ActionCategory Category => ActionCategory.Quality;
    public override int Level => 90;
    public override uint ActionId => 100435;

    public override bool IncreasesQuality => true;
    public override int DurabilityCost => 0;

    public override int CPCost(Simulator s) => 32;
    public override float Efficiency(Simulator s) => 1.00f;

    public override bool CanUse(Simulator s) =>
        s.GetEffectStrength(EffectType.InnerQuiet) == 10
        && base.CanUse(s);
}
