namespace Craftimizer.Simulator.Actions;

internal sealed class Groundwork : BaseAction
{
    public override ActionCategory Category => ActionCategory.Synthesis;
    public override int Level => 72;
    public override uint ActionId => 100403;

    public override bool IncreasesProgress => true;
    public override int DurabilityCost => 20;

    public override int CPCost(Simulator s) => 18;
    public override int Efficiency(Simulator s)
    {
        // Groundwork Mastery Trait
        var ret = s.Input.Stats.Level >= 86 ? 360 : 300;
        return s.Durability < s.CalculateDurabilityCost(DurabilityCost) ? ret / 2 : ret;
    }
}
