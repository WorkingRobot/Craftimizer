namespace Craftimizer.Simulator.Actions;

internal sealed class Veneration : BaseBuffAction
{
    public Veneration()
    {
        Category = ActionCategory.Buffs;
        Level = 15;
        ActionId = 19297;
        Effect = EffectType.Veneration;
        Duration = 4;
    }

    public override void CPCost(Simulator s, ref int cost)
    {
        cost = 18;
    }
}
