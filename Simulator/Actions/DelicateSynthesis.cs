namespace Craftimizer.Simulator.Actions;

internal sealed class DelicateSynthesis : BaseAction
{
    public override ActionCategory Category => ActionCategory.Synthesis;
    public override int Level => 76;
    public override uint ActionId => 100323;

    public override int CPCost => 32;
    public override float Efficiency => 1.00f;
    public override bool IncreasesProgress => true;
    public override bool IncreasesQuality => true;
}
