namespace Craftimizer.Simulator.Actions;

internal sealed class AdvancedTouch() : BaseAction(
    ActionCategory.Quality, level: 84, actionId: 100411,
    increasesQuality: true,
    defaultCPCost: 46, defaultEfficiency: 150)
{
    public override int CPCost(Simulator s) =>
        s.ActionStates.TouchComboIdx == 2 ? 18 : 46;
}
