namespace Craftimizer.Simulator.Actions;

internal sealed class MastersMend : BaseAction
{
    public MastersMend()
    {
        Category = ActionCategory.Durability;
        Level = 7;
        ActionId = 100003;
        DurabilityCost = 0;
    }

    public override void CPCost(Simulator s, ref int cost)
    {
        cost = 88;
    }

    public override void UseSuccess(Simulator s, ref int eff) =>
        s.RestoreDurability(30);
}
