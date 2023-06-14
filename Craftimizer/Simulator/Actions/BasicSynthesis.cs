namespace Craftimizer.Simulator.Actions;

internal class BasicSynthesis : BaseAction
{
    public BasicSynthesis(Simulation simulation) : base(simulation) { }

    public override ActionCategory Category => ActionCategory.Synthesis;
    public override int Level => 1;
    public override int ActionId => 100001;

    public override int CPCost => 0;
    // Basic Synthesis Mastery Trait
    public override float Efficiency => Simulation.Stats.Level >= 31 ? 1.20f : 1.00f;
    public override bool IncreasesProgress => true;
}
