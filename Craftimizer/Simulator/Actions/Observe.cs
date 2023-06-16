namespace Craftimizer.Simulator.Actions;

internal class Observe : BaseAction
{
    public override ActionCategory Category => ActionCategory.Other;
    public override int Level => 13;
    public override uint ActionId => 100010;

    public override int CPCost => 7;
    public override int DurabilityCost => 0;
}
