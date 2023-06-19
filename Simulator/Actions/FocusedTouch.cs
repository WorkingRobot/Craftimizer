namespace Craftimizer.Simulator.Actions;

internal sealed class FocusedTouch : BaseAction
{
    public override ActionCategory Category => ActionCategory.Quality;
    public override int Level => 68;
    public override uint ActionId => 100243;

    public override int CPCost => 18;
    public override float Efficiency => 1.50f;
    public override bool IncreasesQuality => true;
    public override float SuccessRate => Simulation.IsPreviousAction(ActionType.Observe) ? 1.00f : 0.50f;
}
