namespace Craftimizer.Simulator.Actions;

internal sealed class StandardTouch : BaseAction
{
    public override ActionCategory Category => ActionCategory.Quality;
    public override int Level => 18;
    public override uint ActionId => 100004;

    public override bool IncreasesQuality => true;

    public override int CPCost(Simulator s) => s.ActionStates.TouchComboIdx == 1 ? 18 : 32;
    public override float Efficiency(Simulator s) => 1.25f;
}
