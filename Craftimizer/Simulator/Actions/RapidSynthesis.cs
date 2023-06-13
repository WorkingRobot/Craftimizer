namespace Craftimizer.Simulator.Actions;

internal class RapidSynthesis : BaseAction
{
    public RapidSynthesis(Simulation simulation) : base(simulation) { }

    public override ActionCategory Category => ActionCategory.Synthesis;
    public override int Level => 9;

    public override int CPCost => 0;
    // Rapid Synthesis Mastery Trait
    public override float Efficiency => Simulation.Stats.Level >= 63 ? 5.00f : 2.50f;
    public override float SuccessRate => 0.50f;

    public override void UseSuccess() =>
        Simulation.IncreaseProgress(Efficiency);
}
