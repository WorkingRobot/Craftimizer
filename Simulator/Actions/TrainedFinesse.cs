namespace Craftimizer.Simulator.Actions;

internal sealed class TrainedFinesse : BaseAction
{
    public override ActionCategory Category => ActionCategory.Quality;
    public override int Level => 90;
    public override uint ActionId => 100435;

    public override bool IncreasesQuality => true;
    public override int DurabilityCost => 0;

    public override int CPCost(Simulator s) => 32;
    public override int Efficiency(Simulator s) => 100;

    public override bool CouldUse(Simulator s) =>
        s.GetEffectStrength(EffectType.InnerQuiet) == 10
        && base.CouldUse(s);
}
