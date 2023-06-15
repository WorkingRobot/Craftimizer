namespace Craftimizer.Simulator.Actions;

internal class PrudentTouch : BaseAction
{
    public PrudentTouch(Simulation simulation) : base(simulation) { }

    public override ActionCategory Category => ActionCategory.Quality;
    public override int Level => 66;
    public override int ActionId => 100227;

    public override int CPCost => 25;
    public override float Efficiency => 1.00f;
    public override bool IncreasesQuality => true;
    public override int DurabilityCost => base.DurabilityCost / 2;

    public override bool CanUse =>
        !(Simulation.HasEffect(EffectType.WasteNot) || Simulation.HasEffect(EffectType.WasteNot2))
        && base.CanUse;
}
