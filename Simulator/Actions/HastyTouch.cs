namespace Craftimizer.Simulator.Actions;

internal sealed class HastyTouch : BaseAction
{
    public HastyTouch()
    {
        Category = ActionCategory.Quality;
        Level = 9;
        ActionId = 100355;
        IncreasesQuality = true;
    }

    public override void CPCost(Simulator s, ref int cost)
    {
        cost = 0;
    }

    public override void Efficiency(Simulator s, ref int eff)
    {
        eff = 100;
    }

    public override void SuccessRate(Simulator s, ref float success)
    {
        success = 0.60f;
    }
}
