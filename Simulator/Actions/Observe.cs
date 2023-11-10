namespace Craftimizer.Simulator.Actions;

internal sealed class Observe : BaseAction
{
    public override ActionCategory Category => ActionCategory.Other;
    public override int Level => 13;
    public override uint ActionId => 100010;

    public override int DurabilityCost => 0;

    public override int CPCost<S>(Simulator<S> s) => 7;
}
