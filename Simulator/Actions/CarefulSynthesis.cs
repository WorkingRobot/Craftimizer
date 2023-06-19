namespace Craftimizer.Simulator.Actions;

internal sealed class CarefulSynthesis : BaseAction
{
    public override ActionCategory Category => ActionCategory.Synthesis;
    public override int Level => 62;
    public override uint ActionId => 100203;

    public override int CPCost => 7;
    // Careful Synthesis Mastery Trait
    public override float Efficiency => Simulation.Input.Stats.Level >= 82 ? 1.80f : 1.50f;
    public override bool IncreasesProgress => true;
}
