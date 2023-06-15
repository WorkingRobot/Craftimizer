namespace Craftimizer.Simulator.Actions;

internal class StandardTouch : BaseAction
{
    public StandardTouch(Simulation simulation) : base(simulation) { }

    public override ActionCategory Category => ActionCategory.Quality;
    public override int Level => 18;
    public override int ActionId => 100004;

    public override int CPCost => Simulation.IsPreviousAction<BasicTouch>() ? 18 : 32;
    public override float Efficiency => 1.25f;
    public override bool IncreasesQuality => true;
}
