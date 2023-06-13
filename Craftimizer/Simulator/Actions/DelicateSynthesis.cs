namespace Craftimizer.Simulator.Actions;

internal class DelicateSynthesis : BaseAction
{
    public DelicateSynthesis(Simulation simulation) : base(simulation) { }

    public override ActionCategory Category => ActionCategory.Synthesis;
    public override int Level => 76;
    public override int ActionId => 100323;

    public override int CPCost => 32;
    public override float Efficiency => 1.00f;

    public override void UseSuccess()
    {
        Simulation.IncreaseQuality(Efficiency);
        Simulation.IncreaseProgress(Efficiency);
    }
}
