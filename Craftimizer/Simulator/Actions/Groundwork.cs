namespace Craftimizer.Simulator.Actions;

internal class Groundwork : BaseAction
{
    public Groundwork(Simulation simulation) : base(simulation) { }

    public override ActionCategory Category => ActionCategory.Synthesis;
    public override int Level => 72;

    public override int CPCost => 18;
    // Groundwork Mastery Trait
    public override float Efficiency => Simulation.Stats.Level >= 86 ? 3.60f : 3.00f;
    public override int DurabilityCost => 20;

    public override void UseSuccess() =>
        Simulation.IncreaseProgress(Efficiency);
}
