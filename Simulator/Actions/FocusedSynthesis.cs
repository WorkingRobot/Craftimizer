namespace Craftimizer.Simulator.Actions;

internal sealed class FocusedSynthesis : BaseAction
{
    public override ActionCategory Category => ActionCategory.Synthesis;
    public override int Level => 67;
    public override uint ActionId => 100235;

    public override bool IncreasesProgress => true;

    public override int CPCost(Simulator s) => 5;
    public override float Efficiency(Simulator s) => 2.00f;
    public override float SuccessRate(Simulator s) => s.ActionStates.Observed ? 1.00f : 0.50f;
}
