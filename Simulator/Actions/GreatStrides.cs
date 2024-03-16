namespace Craftimizer.Simulator.Actions;

internal sealed class GreatStrides() : BaseBuffAction(
    ActionCategory.Buffs, 21, 260,
    EffectType.GreatStrides, duration: 3,
    increasesStepCount: false,
    defaultCPCost: 32)
{

}
