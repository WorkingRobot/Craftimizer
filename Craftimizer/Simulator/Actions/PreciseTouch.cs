namespace Craftimizer.Simulator.Actions;

internal class PreciseTouch : BaseAction
{
    public PreciseTouch(Simulation simulation) : base(simulation) { }

    public override ActionCategory Category => ActionCategory.Quality;
    public override int Level => 53;
    public override int ActionId => 100128;

    public override int CPCost => 18;
    public override float Efficiency => 1.50f;
    public override bool IncreasesQuality => true;

    public override bool CanUse =>
        (Simulation.Condition == Condition.Good || Simulation.Condition == Condition.Excellent)
        && base.CanUse;

    public override void UseSuccess()
    {
        base.UseSuccess();
        Simulation.StrengthenEffect(Effect.InnerQuiet);
    }
}