namespace Craftimizer.Simulator.Actions;

internal sealed class FocusedSynthesisCombo : BaseComboAction<Observe, FocusedSynthesis>
{
    public override ActionType ActionTypeA => ActionType.Observe;
    public override ActionType ActionTypeB => ActionType.FocusedSynthesis;
}
