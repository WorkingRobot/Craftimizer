namespace Craftimizer.Simulator.Actions;

internal sealed class BasicTouch() : BaseAction(
    ActionCategory.Quality, 5, 100002,
    increasesQuality: true,
    defaultCPCost: 18,
    defaultEfficiency: 100)
{

}
