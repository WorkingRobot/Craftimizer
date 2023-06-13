namespace Craftimizer.Simulator.Actions;

internal class BasicTouch : BaseAction
{
    public BasicTouch(Simulation simulation) : base(simulation) { }

    public override ActionCategory Category => ActionCategory.Quality;
    public override int Level => 5;

    public override int CPCost => 18;
    public override float Efficiency => 1.00f;

    public override void UseSuccess() =>
        Simulation.IncreaseQuality(Efficiency);
}
