namespace Craftimizer.Simulator.Actions;

internal sealed class FocusedTouch() : BaseAction(
    ActionCategory.Quality, 68, 100243,
    increasesQuality: true,
    defaultCPCost: 18,
    defaultEfficiency: 150,
    defaultSuccessRate: 0.50f
    )
{
    public override float SuccessRate(Simulator s) =>
        s.ActionStates.Observed ? 1.00f : 0.50f;
}
