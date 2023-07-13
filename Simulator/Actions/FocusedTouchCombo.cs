namespace Craftimizer.Simulator.Actions;

// Observe -> Focused Touch
internal sealed class FocusedTouchCombo : BaseAction
{
    public override ActionCategory Category => ActionCategory.Combo;
    public override int Level => 68;
    public override uint ActionId => 100243;

    public override bool IncreasesQuality => true;

    public override int CPCost(Simulator s) => 7 + 18;

    public override bool CanUse(Simulator s) =>
        //                                 Observe.DurabilityCost v
        base.CanUse(s) && StandardTouchCombo.VerifyDurability2(s, 0);

    private static readonly Observe ActionA = new();
    private static readonly FocusedTouch ActionB = new();
    public override void Use(Simulator s)
    {
        s.ExecuteForced(ActionType.Observe, ActionA);
        ActionB.Use(s);
    }

    public override string GetTooltip(Simulator s, bool addUsability) =>
        $"{ActionA.GetTooltip(s, addUsability)}\n{ActionB.GetTooltip(s, addUsability)}";
}
