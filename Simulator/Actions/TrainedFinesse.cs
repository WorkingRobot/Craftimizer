namespace Craftimizer.Simulator.Actions;

internal sealed class TrainedFinesse : BaseAction
{
    public override ActionCategory Category => ActionCategory.Quality;
    public override int Level => 90;
    public override uint ActionId => 100435;

    public override bool IncreasesQuality => true;
    public override int DurabilityCost => 0;

    public override int CPCost<S>(Simulator<S> s) => 32;
    public override int Efficiency<S>(Simulator<S> s) => 100;

    public override bool CanUse<S>(Simulator<S> s) =>
        s.GetEffectStrength(EffectType.InnerQuiet) == 10
        && base.CanUse(s);
}
