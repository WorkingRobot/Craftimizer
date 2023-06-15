namespace Craftimizer.Simulator.Actions;

internal class AdvancedTouch : BaseAction
{
    public AdvancedTouch(Simulation simulation) : base(simulation) { }

    public override ActionCategory Category => ActionCategory.Quality;
    public override int Level => 84;
    public override int ActionId => 100411;

    public override int CPCost => Simulation.IsPreviousAction<StandardTouch>() && Simulation.IsPreviousAction<BasicTouch>(2) ? 18 : 46;
    public override float Efficiency => 1.50f;
    public override bool IncreasesQuality => true;
}
