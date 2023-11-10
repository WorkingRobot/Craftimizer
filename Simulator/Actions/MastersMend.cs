namespace Craftimizer.Simulator.Actions;

internal sealed class MastersMend : BaseAction
{
    public override ActionCategory Category => ActionCategory.Durability;
    public override int Level => 7;
    public override uint ActionId => 100003;

    public override int DurabilityCost => 0;

    public override int CPCost<S>(Simulator<S> s) => 88;

    public override void UseSuccess<S>(Simulator<S> s) =>
        s.RestoreDurability(30);
}
