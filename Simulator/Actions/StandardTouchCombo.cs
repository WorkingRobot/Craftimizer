namespace Craftimizer.Simulator.Actions;

internal sealed class StandardTouchCombo : BaseComboAction<BasicTouch, StandardTouch>
{
    public override ActionType ActionTypeA => ActionType.BasicTouch;
    public override ActionType ActionTypeB => ActionType.StandardTouch;

    public override int CPCost(Simulator s) => 18 * 2;
}
