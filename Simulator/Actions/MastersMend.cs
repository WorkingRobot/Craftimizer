namespace Craftimizer.Simulator.Actions;

internal sealed class MastersMend() : BaseAction(
    ActionCategory.Durability, 7, 100003,
    macroWaitTime: 2,
    durabilityCost: 0,
    defaultCPCost: 88
    )
{
    public override void UseSuccess(Simulator s) =>
        s.RestoreDurability(30);
}
