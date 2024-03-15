namespace Craftimizer.Simulator.Actions;

internal abstract class BaseComboAction<A, B> : BaseComboAction where A : BaseAction, new() where B : BaseAction, new()
{
    protected static readonly A ActionA = new();
    protected static readonly B ActionB = new();

    protected BaseComboAction()
    {
        Level = ActionB.Level;
        ActionId = ActionB.ActionId;
        IncreasesProgress = ActionA.IncreasesProgress || ActionB.IncreasesProgress;
        IncreasesQuality = ActionA.IncreasesQuality || ActionB.IncreasesQuality;
    }

    public override void CPCost(Simulator s, ref int cost)
    {
        var costTmp = 0;
        ActionA.CPCost(s, ref costTmp);
        cost += costTmp;
        ActionB.CPCost(s, ref costTmp);
        cost += costTmp;
    }

    public override bool IsPossible(Simulator s) => ActionA.IsPossible(s) && ActionB.IsPossible(s);

    public override bool CouldUse(Simulator s, ref int cost) =>
        BaseCouldUse(s, ref cost) && VerifyDurability2(s, ActionA.DurabilityCost);

    public override void Use(Simulator s, ref int cost, ref float success, ref int eff)
    {
        ActionA.Use(s, ref cost, ref success, ref eff);
        ActionB.Use(s, ref cost, ref success, ref eff);
    }

    public override string GetTooltip(Simulator s, bool addUsability) =>
        $"{ActionA.GetTooltip(s, addUsability)}\n\n{ActionB.GetTooltip(s, addUsability)}";
}
