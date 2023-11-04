namespace Craftimizer.Simulator.Actions;

internal sealed class DelicateSynthesis : BaseAction
{
    public override ActionCategory Category => ActionCategory.Synthesis;
    public override int Level => 76;
    public override uint ActionId => 100323;

    public override bool IncreasesProgress => true;
    public override bool IncreasesQuality => true;

    public override int CPCost(Simulator s) => 32;
    public override int Efficiency(Simulator s) => 100;
}
