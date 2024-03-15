namespace Craftimizer.Simulator.Actions;

internal sealed class Innovation : BaseBuffAction
{
    public Innovation()
    {
        Level = 26;
        Effect = EffectType.Innovation;
        MacroWaitTime = 3;
        ActionId = 19004;
        Category = ActionCategory.Buffs;
        Duration = 4;
    }

    public override void CPCost(Simulator s, ref int cost)
    {
        cost = 18;
    }
}
