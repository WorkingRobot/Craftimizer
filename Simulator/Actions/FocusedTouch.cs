namespace Craftimizer.Simulator.Actions;

internal sealed class FocusedTouch() : BaseAction(
    ActionCategory.Quality, 68, 100243,
    increasesQuality: true,
    defaultCPCost: 18,
    defaultEfficiency: 150,
    defaultSuccessRate: 50
    )
{
    public override int SuccessRate(Simulator s) =>
        s.ActionStates.Observed ? 100 : 50;
}
