namespace Craftimizer.Simulator.Actions;

internal sealed class FocusedSynthesis() : BaseAction(
    ActionCategory.Synthesis, 67, 100235,
    increasesProgress: true,
    defaultCPCost: 5,
    defaultEfficiency: 200,
    defaultSuccessRate: 50
    )
{
    public override int SuccessRate(Simulator s) =>
        s.ActionStates.Observed ? 100 : 50;
}
