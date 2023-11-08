namespace Craftimizer.Simulator.Actions;

internal sealed class FocusedTouch : BaseAction
{
    public override ActionCategory Category => ActionCategory.Quality;
    public override int Level => 68;
    public override uint ActionId => 100243;

    public override bool IncreasesQuality => true;

    public override int CPCost(Simulator s) => 18;
    public override int Efficiency(Simulator s) => 150;
    public override float SuccessRate(Simulator s) => s.ActionStates.Observed ? 1.00f : 0.50f;
}
