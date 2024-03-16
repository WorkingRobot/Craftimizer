namespace Craftimizer.Simulator.Actions;

internal sealed class DelicateSynthesis() : BaseAction(
    ActionCategory.Synthesis, 76, 100323,
    increasesProgress: true, increasesQuality: true,
    defaultCPCost: 32,
    defaultEfficiency: 100
    )
{

}
