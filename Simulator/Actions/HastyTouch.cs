namespace Craftimizer.Simulator.Actions;

internal sealed class HastyTouch : BaseAction
{
    public override ActionCategory Category => ActionCategory.Quality;
    public override int Level => 9;
    public override uint ActionId => 100355;

    public override int CPCost => 0;
    public override float Efficiency => 1.00f;
    public override bool IncreasesQuality => true;
    public override float SuccessRate => 0.60f;
}
