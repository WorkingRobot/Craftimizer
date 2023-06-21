namespace Craftimizer.Simulator.Actions;

internal sealed class RapidSynthesis : BaseAction
{
    public override ActionCategory Category => ActionCategory.Synthesis;
    public override int Level => 9;
    public override uint ActionId => 100363;

    public override bool IncreasesProgress => true;

    public override int CPCost(Simulator s) => 0;
    // Rapid Synthesis Mastery Trait
    public override float Efficiency(Simulator s) => s.Input.Stats.Level >= 63 ? 5.00f : 2.50f;
    public override float SuccessRate(Simulator s) => 0.50f;
}
