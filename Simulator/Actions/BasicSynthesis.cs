namespace Craftimizer.Simulator.Actions;

internal sealed class BasicSynthesis : BaseAction
{
    public override ActionCategory Category => ActionCategory.Synthesis;
    public override int Level => 1;
    public override uint ActionId => 100001;

    public override bool IncreasesProgress => true;

    public override int CPCost(Simulator s) => 0;
    // Basic Synthesis Mastery Trait
    public override int Efficiency(Simulator s) => s.Input.Stats.Level >= 31 ? 120 : 100;
}
