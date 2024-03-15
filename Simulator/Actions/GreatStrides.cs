namespace Craftimizer.Simulator.Actions;

internal sealed class GreatStrides : BaseBuffAction
{
    public int CP = 32;

    public GreatStrides()
    {
        Category = ActionCategory.Buffs;
        Level = 21;
        ActionId = 260;
        Effect = EffectType.GreatStrides;
        Duration = 3;
    }

    public override void CPCost(Simulator s, ref int cost)
    {
        cost = CP;
    }
}
