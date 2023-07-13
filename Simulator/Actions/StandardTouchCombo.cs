namespace Craftimizer.Simulator.Actions;

// Basic Touch -> Standard Touch
internal sealed class StandardTouchCombo : BaseAction
{
    public override ActionCategory Category => ActionCategory.Combo;
    public override int Level => 18;
    public override uint ActionId => 100004;

    public override bool IncreasesQuality => true;

    public override int CPCost(Simulator s) => 18 + 18;

    public override bool CanUse(Simulator s) =>
        //           BasicTouch.DurabilityCost vv
        base.CanUse(s) && VerifyDurability2(s, 10);

    private static readonly BasicTouch ActionA = new();
    private static readonly StandardTouch ActionB = new();
    public override void Use(Simulator s)
    {
        s.ExecuteForced(ActionType.BasicTouch, ActionA);
        ActionB.Use(s);
    }

    public override string GetTooltip(Simulator s, bool addUsability) =>
        $"{ActionA.GetTooltip(s, addUsability)}\n{ActionB.GetTooltip(s, addUsability)}";
}
