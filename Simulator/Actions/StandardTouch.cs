namespace Craftimizer.Simulator.Actions;

internal sealed class StandardTouch : BaseAction
{
    public StandardTouch()
    {
        Category = ActionCategory.Quality;
        Level = 18;
        ActionId = 100004;
        IncreasesQuality = true;
    }

    public override void CPCost(Simulator s, ref int cost)
    {
        cost = s.ActionStates.TouchComboIdx == 1 ? 18 : 32;
    }

    public override void Efficiency(Simulator s, ref int eff)
    {
        eff = 125;
    }
}
