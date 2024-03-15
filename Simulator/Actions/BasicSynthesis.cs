namespace Craftimizer.Simulator.Actions;

internal sealed class BasicSynthesis : BaseAction
{
    public int CP;
    public int EfficiencyNormal = 100;
    public int EfficiencyGood = 120;

    public BasicSynthesis()
    {
        Category = ActionCategory.Synthesis;
        IncreasesProgress = true;
        ActionId = 100001;
        Level = 1;
    }

    public override void CPCost(Simulator s, ref int cost)
    {
        cost = CP;
    }
    // Basic Synthesis Mastery Trait
    public override void Efficiency(Simulator s, ref int eff)
    {
        eff = s.Input.Stats.Level >= 31 ? EfficiencyGood : EfficiencyNormal;
    }
}
