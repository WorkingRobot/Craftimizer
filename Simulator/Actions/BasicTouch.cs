namespace Craftimizer.Simulator.Actions;

internal sealed class BasicTouch : BaseAction
{
    public int CP = 18;
    public int eff = 100;

    public BasicTouch()
    {
        Category = ActionCategory.Quality;
        Level = 5;
        ActionId = 100002;
        IncreasesQuality = true;
    }

    public override void CPCost(Simulator s, ref int cost)
    {
        cost = CP;
    }

    public override void Efficiency(Simulator s, ref int eff)
    {
        eff = this.eff;
    }
}
