namespace Craftimizer.Simulator.Actions;

internal sealed class Observe : BaseAction
{
    public Observe()
    {
        Category = ActionCategory.Other;
        Level = 13;
        ActionId = 100010;
        DurabilityCost = 0;
    }

    public override void CPCost(Simulator s, ref int cost)
    {
        cost = 7;
    }
}
