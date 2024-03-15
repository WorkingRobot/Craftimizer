namespace Craftimizer.Simulator.Actions;

internal sealed class WasteNot : BaseBuffAction
{
    public WasteNot()
    {
        Category = ActionCategory.Durability;
        Level = 15;
        ActionId = 4631;
        Effect = EffectType.WasteNot;
        Duration = 4;
    }

    public override void CPCost(Simulator s, ref int cost)
    {
        cost = 56;
    }

    public override void UseSuccess(Simulator s, ref int eff)
    {
        base.UseSuccess(s, ref eff);
        s.RemoveEffect(EffectType.WasteNot2);
    }
}
