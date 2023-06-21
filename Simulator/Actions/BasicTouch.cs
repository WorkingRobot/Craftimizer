namespace Craftimizer.Simulator.Actions;

internal sealed class BasicTouch : BaseAction
{
    public override ActionCategory Category => ActionCategory.Quality;
    public override int Level => 5;
    public override uint ActionId => 100002;

    public override bool IncreasesQuality => true;

    public override int CPCost(Simulator s) => 18;
    public override float Efficiency(Simulator s) => 1.00f;
}
