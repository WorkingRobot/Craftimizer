namespace Craftimizer.Simulator.Actions;

internal sealed class AdvancedTouchCombo : BaseComboAction<StandardTouchCombo, AdvancedTouch>
{
    public override ActionType ActionTypeA => ActionType.StandardTouchCombo;
    public override ActionType ActionTypeB => ActionType.AdvancedTouch;
}
