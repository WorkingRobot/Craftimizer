namespace Craftimizer.Simulator.Actions;

internal sealed class FocusedSynthesis : BaseAction
{
    public int CP = 5;
    public int Eff = 200;
    public float SuccessNormal = 0.50f;
    public float SuccessObserved = 1.00f;

    public FocusedSynthesis()
    {
        Category = ActionCategory.Synthesis;
        Level = 67;
        ActionId = 100235;
        IncreasesProgress = true;
    }

    public override void CPCost(Simulator s, ref int cost)
    {
        cost = CP;
    }

    public override void Efficiency(Simulator s, ref int eff)
    {
        eff = Eff;
    }

    public override void SuccessRate(Simulator s, ref float success)
    {
        success = s.ActionStates.Observed ? SuccessObserved : SuccessNormal;
    }
}
