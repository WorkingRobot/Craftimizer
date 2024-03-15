namespace Craftimizer.Simulator.Actions;

internal sealed class RapidSynthesis : BaseAction
{
    public RapidSynthesis()
    {
        Category = ActionCategory.Synthesis;
        Level = 9;
        ActionId = 100363;
        IncreasesProgress = true;
    }

    public override void CPCost(Simulator s, ref int cost)
    {
        cost = 0;
    }

    // Rapid Synthesis Mastery Trait
    public override void Efficiency(Simulator s, ref int eff)
    {
        eff = s.Input.Stats.Level >= 63 ? 500 : 250;
    }

    public override void SuccessRate(Simulator s, ref float success)
    {
        success = 0.50f;
    }
}
