namespace Craftimizer.Simulator.Actions;

internal sealed class AdvancedTouch : BaseAction
{
    public int CostDefault = 46;
    public int CostOptimal = 18;
    public int EfficiencyDefault = 150;

    public AdvancedTouch()
    {
        Category = ActionCategory.Quality;
        Level = 84;
        ActionId = 100411;
        IncreasesQuality = true;
    }


    public override void CPCost(Simulator s, ref int cost)
    {
        cost = s.ActionStates.TouchComboIdx == 2 ? CostOptimal : CostDefault;
    }

    public override void Efficiency(Simulator s, ref int eff)
    {
        eff = EfficiencyDefault;
    }
}
