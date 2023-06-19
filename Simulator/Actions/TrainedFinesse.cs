namespace Craftimizer.Simulator.Actions;

internal sealed class TrainedFinesse : BaseAction
{
    public override ActionCategory Category => ActionCategory.Quality;
    public override int Level => 90;
    public override uint ActionId => 100435;

    public override int CPCost => 32;
    public override float Efficiency => 1.00f;
    public override bool IncreasesQuality => true;
    public override int DurabilityCost => 0;

    public override bool CanUse =>
        Simulation.GetEffectStrength(EffectType.InnerQuiet) == 10
        && base.CanUse;
}
