namespace Craftimizer.Simulator.Actions;

internal sealed class StandardTouchCombo : BaseComboAction<BasicTouch, StandardTouch>
{
    public override ActionType ActionTypeA => ActionType.BasicTouch;
    public override ActionType ActionTypeB => ActionType.StandardTouch;
}
