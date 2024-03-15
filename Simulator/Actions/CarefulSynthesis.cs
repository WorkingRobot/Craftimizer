namespace Craftimizer.Simulator.Actions;

internal sealed class CarefulSynthesis : BaseAction
{
    public int CP = 7;
    public int EfficiencyNormal = 150;
    public int EfficiencyMastery = 180;

    public CarefulSynthesis()
    {
        Category = ActionCategory.Synthesis;
        Level = 62;
        ActionId = 100203;
        IncreasesProgress = true;
    }

    public override void CPCost(Simulator s, ref int cost)
    {
        cost = CP;
    }

    // Careful Synthesis Mastery Trait
    public override void Efficiency(Simulator s, ref int eff)
    {
        eff = s.Input.Stats.Level >= 82 ? EfficiencyMastery : EfficiencyNormal;
    }
}
