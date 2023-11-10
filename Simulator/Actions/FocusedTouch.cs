namespace Craftimizer.Simulator.Actions;

internal sealed class FocusedTouch : BaseAction
{
    public override ActionCategory Category => ActionCategory.Quality;
    public override int Level => 68;
    public override uint ActionId => 100243;

    public override bool IncreasesQuality => true;

    public override int CPCost<S>(Simulator<S> s) => 18;
    public override int Efficiency<S>(Simulator<S> s) => 150;
    public override float SuccessRate<S>(Simulator<S> s) => s.ActionStates.Observed ? 1.00f : 0.50f;
}
