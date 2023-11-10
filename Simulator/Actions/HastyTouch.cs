namespace Craftimizer.Simulator.Actions;

internal sealed class HastyTouch : BaseAction
{
    public override ActionCategory Category => ActionCategory.Quality;
    public override int Level => 9;
    public override uint ActionId => 100355;

    public override bool IncreasesQuality => true;

    public override int CPCost<S>(Simulator<S> s) => 0;
    public override int Efficiency<S>(Simulator<S> s) => 100;
    public override float SuccessRate<S>(Simulator<S> s) => 0.60f;
}
