namespace Craftimizer.Simulator.Actions;

internal sealed class WasteNot() : BaseBuffAction(
    ActionCategory.Durability, 15, 4631,
    EffectType.WasteNot, duration: 4,
    defaultCPCost: 56
    )
{
    public override void UseSuccess(Simulator s)
    {
        base.UseSuccess(s);
        s.RemoveEffect(EffectType.WasteNot2);
    }
}
