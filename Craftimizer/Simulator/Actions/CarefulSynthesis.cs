namespace Craftimizer.Simulator.Actions;

internal class CarefulSynthesis : BaseAction
{
    public CarefulSynthesis(Simulation simulation) : base(simulation) { }

    public override ActionCategory Category => ActionCategory.Synthesis;
    public override int Level => 62;
    public override int ActionId => 100203;

    public override int CPCost => 7;
    // Careful Synthesis Mastery Trait
    public override float Efficiency => Simulation.Stats.Level >= 82 ? 1.80f : 1.50f;

    public override void UseSuccess() =>
        Simulation.IncreaseProgress(Efficiency);
}
