namespace Craftimizer.Simulator.Actions;

// Basic Touch -> Standard Touch -> Advanced Touch
internal sealed class AdvancedTouchCombo : BaseAction
{
    public override ActionCategory Category => ActionCategory.Combo;
    public override int Level => 84;
    public override uint ActionId => 100411;

    public override bool IncreasesQuality => true;

    public override int CPCost(Simulator s) => 18 + 18 + 18;

    public override bool CanUse(Simulator s) =>
        //           BasicTouch.DurabilityCost vv  vv StandardTouch.DurabilityCost
        base.CanUse(s) && VerifyDurability3(s, 10, 10);

    private static readonly BasicTouch ActionA = new();
    private static readonly StandardTouch ActionB = new();
    private static readonly AdvancedTouch ActionC = new();
    public override void Use(Simulator s)
    {
        s.ExecuteForced(ActionType.BasicTouch, ActionA);
        s.ExecuteForced(ActionType.StandardTouch, ActionB);
        ActionC.Use(s);
    }

    public override string GetTooltip(Simulator s, bool addUsability) =>
        $"{ActionA.GetTooltip(s, addUsability)}\n{ActionB.GetTooltip(s, addUsability)}\n{ActionC.GetTooltip(s, addUsability)}";
}
