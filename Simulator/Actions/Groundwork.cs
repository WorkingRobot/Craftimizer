namespace Craftimizer.Simulator.Actions;

internal sealed class Groundwork() : BaseAction(
    ActionCategory.Synthesis, 72, 100403,
    increasesProgress: true,
    durabilityCost: 20,
    defaultCPCost: 18,
    defaultEfficiency: 300
    )
{
    public override int Efficiency(Simulator s)
    {
        // Groundwork Mastery Trait
        var eff = s.Input.Stats.Level >= 86 ? 360 : 300;
        if (s.Durability < s.CalculateDurabilityCost(DurabilityCost))
            eff /= 2;
        return eff;
    }
}
