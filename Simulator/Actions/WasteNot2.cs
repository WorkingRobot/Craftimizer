namespace Craftimizer.Simulator.Actions;

internal sealed class WasteNot2() : BaseBuffAction(
    ActionCategory.Durability, 47, 4639,
    EffectType.WasteNot2, duration: 8,
    defaultCPCost: 98
    )
{
    public override void UseSuccess(RotationSimulator s)
    {
        base.UseSuccess(s);
        s.RemoveEffect(EffectType.WasteNot);
    }
}
