namespace Craftimizer.Simulator.Actions;

internal sealed class FocusedTouchCombo : BaseComboAction<Observe, FocusedTouch>
{
    public override ActionType ActionTypeA => ActionType.Observe;
    public override ActionType ActionTypeB => ActionType.FocusedTouch;
}
