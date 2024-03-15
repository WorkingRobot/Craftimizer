namespace Craftimizer.Simulator.Actions;

internal sealed class WasteNot2 : BaseBuffAction
{
    public WasteNot2()
    {
        Category = ActionCategory.Durability;
        Level = 47;
        ActionId = 4639;
        Effect = EffectType.WasteNot2;
        Duration = 8;
    }

    public override void CPCost(Simulator s, ref int cost)
    {
        cost = 98;
    }

    public override void UseSuccess(Simulator s, ref int eff)
    {
        base.UseSuccess(s, ref eff);
        s.RemoveEffect(EffectType.WasteNot);
    }
}
