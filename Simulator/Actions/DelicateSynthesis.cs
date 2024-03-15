namespace Craftimizer.Simulator.Actions;

internal sealed class DelicateSynthesis : BaseAction
{
    public int CP = 32;
    public int Eff = 100;

    public DelicateSynthesis()
    {
        Category = ActionCategory.Synthesis;
        Level = 76;
        ActionId = 100323;
        IncreasesProgress = true;
        IncreasesQuality = true;
    }

    public override void CPCost(Simulator s, ref int cost)
    {
        cost = CP;
    }

    public override void Efficiency(Simulator s, ref int eff)
    {
        eff = Eff;
    }
}
