namespace Craftimizer.Simulator.Actions;

internal sealed class AdvancedTouchCombo() : BaseComboAction<StandardTouchCombo, AdvancedTouch>(
    ActionType.StandardTouchCombo, ActionType.AdvancedTouch, 18 * 3
    )
{
    public override bool CouldUse(Simulator s) =>
        BaseCouldUse(s) && VerifyDurability3(s, StandardTouchCombo.ActionA.DurabilityCost, StandardTouchCombo.ActionB.DurabilityCost);
}
