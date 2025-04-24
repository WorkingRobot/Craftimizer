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

    public override bool IsPossible(RotationSimulator s) => ActionA.IsPossible(s) && ActionB.IsPossible(s);

    public override bool CouldUse(RotationSimulator s) =>
        BaseCouldUse(s) && VerifyDurability2(s, ActionA.DurabilityCost);

    public override void Use(RotationSimulator s)
    {
        ActionA.Use(s);
        ActionB.Use(s);
    }

    public override string GetTooltip(RotationSimulator s, bool addUsability) =>
        $"{ActionA.GetTooltip(s, addUsability)}\n\n{ActionB.GetTooltip(s, addUsability)}";
}
