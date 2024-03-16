namespace Craftimizer.Simulator.Actions;

internal sealed class FocusedSynthesis() : BaseAction(
    ActionCategory.Synthesis, 67, 100235,
    increasesProgress: true,
    defaultCPCost: 5,
    defaultEfficiency: 200,
    defaultSuccessRate: 0.50f
    )
{
    public override float SuccessRate(Simulator s) =>
        s.ActionStates.Observed ? 1.00f : 0.50f;
}
