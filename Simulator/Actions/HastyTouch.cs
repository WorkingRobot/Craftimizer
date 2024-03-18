namespace Craftimizer.Simulator.Actions;

internal sealed class HastyTouch() : BaseAction(
    ActionCategory.Quality, 9, 100355,
    increasesQuality: true,
    defaultCPCost: 0,
    defaultEfficiency: 100,
    defaultSuccessRate: 60
    )
{

}
