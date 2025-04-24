namespace Craftimizer.Simulator.Actions;

internal sealed class ImmaculateMend() : BaseAction(
    ActionCategory.Durability, 98, 100467,
    durabilityCost: 0,
    defaultCPCost: 112
    )
{
    public override void UseSuccess(RotationSimulator s) =>
        s.RestoreAllDurability();
}
