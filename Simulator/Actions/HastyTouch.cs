namespace Craftimizer.Simulator.Actions;

internal sealed class HastyTouch : BaseAction
{
    public override ActionCategory Category => ActionCategory.Quality;
    public override int Level => 9;
    public override uint ActionId => 100355;

    public override bool IncreasesQuality => true;

    public override int CPCost(Simulator s) => 0;
    public override float Efficiency(Simulator s) => 1.00f;
    public override float SuccessRate(Simulator s) => 0.60f;
}
