namespace Craftimizer.Simulator.Actions;

internal sealed class MastersMend : BaseAction
{
    public override ActionCategory Category => ActionCategory.Durability;
    public override int Level => 7;
    public override uint ActionId => 100003;

    public override int DurabilityCost => 0;

    public override int CPCost(Simulator s) => 88;

    public override void UseSuccess(Simulator s) =>
        s.RestoreDurability(30);
}
