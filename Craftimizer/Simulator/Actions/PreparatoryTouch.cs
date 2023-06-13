namespace Craftimizer.Simulator.Actions;

internal class PreparatoryTouch : BaseAction
{
    public PreparatoryTouch(Simulation simulation) : base(simulation) { }

    public override ActionCategory Category => ActionCategory.Quality;
    public override int Level => 71;
    public override int ActionId => 100299;

    public override int CPCost => 40;
    public override float Efficiency => 2.00f;
    public override int DurabilityCost => 20;

    public override void UseSuccess()
    {
        Simulation.IncreaseQuality(Efficiency);
        Simulation.StrengthenEffect(Effect.InnerQuiet);
    }
}
