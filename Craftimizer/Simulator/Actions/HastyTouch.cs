namespace Craftimizer.Simulator.Actions;

internal class HastyTouch : BaseAction
{
    public HastyTouch(Simulation simulation) : base(simulation) { }

    public override ActionCategory Category => ActionCategory.Quality;
    public override int Level => 9;
    public override int ActionId => 100355;

    public override int CPCost => 0;
    public override float Efficiency => 1.00f;
    public override float SuccessRate => 0.60f;

    public override void UseSuccess() =>
        Simulation.IncreaseQuality(Efficiency);
}
