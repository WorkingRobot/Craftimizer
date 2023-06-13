namespace Craftimizer.Simulator.Actions;

internal class FocusedSynthesis : BaseAction
{
    public FocusedSynthesis(Simulation simulation) : base(simulation) { }

    public override ActionCategory Category => ActionCategory.Synthesis;
    public override int Level => 67;
    public override int ActionId => 100235;

    public override int CPCost => 5;
    public override float Efficiency => 2.00f;
    public override float SuccessRate => Simulation.GetPreviousAction() is Observe ? 1.00f : 0.50f;

    public override void UseSuccess() =>
        Simulation.IncreaseProgress(Efficiency);
}
