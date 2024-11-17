namespace Craftimizer.Simulator.Actions;

internal sealed class AdvancedTouch() : BaseAction(
    ActionCategory.Quality, level: 68, actionId: 100411,
    increasesQuality: true,
    defaultCPCost: 46, defaultEfficiency: 150)
{
    public override int CPCost(Simulator s) =>
        (s.ActionStates.Combo == ActionProc.AdvancedTouch) ? 18 : 46;
}
