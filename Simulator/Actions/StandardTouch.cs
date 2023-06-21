namespace Craftimizer.Simulator.Actions;

internal sealed class StandardTouch : BaseAction
{
    public override ActionCategory Category => ActionCategory.Quality;
    public override int Level => 18;
    public override uint ActionId => 100004;

    public override int CPCost => Simulation.ActionStates.TouchComboIdx == 1 ? 18 : 32;
    public override float Efficiency => 1.25f;
    public override bool IncreasesQuality => true;
}
