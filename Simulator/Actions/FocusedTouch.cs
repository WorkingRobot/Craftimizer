namespace Craftimizer.Simulator.Actions;

internal sealed class FocusedTouch : BaseAction
{
    public int CP = 18;
    public int Eff = 150;
    public float SuccessNormal = 0.50f;
    public float SuccessObserved = 1.00f;

    public FocusedTouch()
    {
        Category = ActionCategory.Quality;
        Level = 68;
        ActionId = 100243;
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

    public override void SuccessRate(Simulator s, ref float success)
    {
        success = s.ActionStates.Observed ? SuccessObserved : SuccessNormal;
    }
}
