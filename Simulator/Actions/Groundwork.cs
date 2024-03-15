namespace Craftimizer.Simulator.Actions;

internal sealed class Groundwork : BaseAction
{
    public Groundwork()
    {
        Category = ActionCategory.Synthesis;
        Level = 72;
        ActionId = 100403;
        IncreasesProgress = true;
        DurabilityCost = 20;
    }

    public override void CPCost(Simulator s, ref int cost)
    {
        cost = 18;
    }

    public override void Efficiency(Simulator s, ref int eff)
    {
        // Groundwork Mastery Trait
        eff = s.Input.Stats.Level >= 86 ? 360 : 300;
        if (s.Durability < s.CalculateDurabilityCost(DurabilityCost))
            eff /= 2;
    }
}
