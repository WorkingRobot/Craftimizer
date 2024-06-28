namespace Craftimizer.Simulator.Actions;

internal sealed class ImmaculateMend() : BaseAction(
    ActionCategory.Durability, 98, 100467,
    macroWaitTime: 2,
    durabilityCost: 0,
    defaultCPCost: 112
    )
{
    public override void UseSuccess(Simulator s) =>
        s.RestoreAllDurability();
}
