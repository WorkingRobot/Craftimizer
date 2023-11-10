namespace Craftimizer.Simulator.Actions;

internal abstract class BaseComboAction<A, B> : BaseComboAction where A : BaseAction, new() where B : BaseAction, new()
{
    protected static readonly A ActionA = new();
    protected static readonly B ActionB = new();

    public override int Level => ActionB.Level;
    public override uint ActionId => ActionB.ActionId;

    public override bool IncreasesProgress => ActionA.IncreasesProgress || ActionB.IncreasesProgress;
    public override bool IncreasesQuality => ActionA.IncreasesQuality || ActionB.IncreasesQuality;

    public override int CPCost<S>(Simulator<S> s) => ActionA.CPCost(s) + ActionB.CPCost(s);

    public override bool CanUse<S>(Simulator<S> s) =>
        BaseCanUse(s) && VerifyDurability2(s, ActionA.DurabilityCost);

    public override void Use<S>(Simulator<S> s)
    {
        ActionA.Use(s);
        ActionB.Use(s);
    }

    public override string GetTooltip<S>(Simulator<S> s, bool addUsability) =>
        $"{ActionA.GetTooltip(s, addUsability)}\n\n{ActionB.GetTooltip(s, addUsability)}";
}
