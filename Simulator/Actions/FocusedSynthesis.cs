namespace Craftimizer.Simulator.Actions;

internal sealed class FocusedSynthesis : BaseAction
{
    public override ActionCategory Category => ActionCategory.Synthesis;
    public override int Level => 67;
    public override uint ActionId => 100235;

    public override bool IncreasesProgress => true;

    public override int CPCost<S>(Simulator<S> s) => 5;
    public override int Efficiency<S>(Simulator<S> s) => 200;
    public override float SuccessRate<S>(Simulator<S> s) => s.ActionStates.Observed ? 1.00f : 0.50f;
}
