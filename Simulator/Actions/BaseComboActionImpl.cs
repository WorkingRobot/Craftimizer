namespace Craftimizer.Simulator.Actions;

internal abstract class BaseComboAction<A, B>(
    ActionType actionTypeA, ActionType actionTypeB,
    int? baseCPCost = null) :
    BaseComboAction(
        actionTypeA, actionTypeB,
        ActionA, ActionB,
        baseCPCost
        ) where A : BaseAction, new() where B : BaseAction, new()
{
    protected static readonly A ActionA = new();
    protected static readonly B ActionB = new();

    public override bool IsPossible(Simulator s) => ActionA.IsPossible(s) && ActionB.IsPossible(s);

    public override bool CouldUse(Simulator s) =>
        BaseCouldUse(s) && VerifyDurability2(s, ActionA.DurabilityCost);

    public override void Use(Simulator s)
    {
        ActionA.Use(s);
        ActionB.Use(s);
    }

    public override string GetTooltip(Simulator s, bool addUsability) =>
        $"{ActionA.GetTooltip(s, addUsability)}\n\n{ActionB.GetTooltip(s, addUsability)}";
}
