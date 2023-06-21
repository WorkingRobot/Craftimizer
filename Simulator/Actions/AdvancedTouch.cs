namespace Craftimizer.Simulator.Actions;

internal sealed class AdvancedTouch : BaseAction
{
    public override ActionCategory Category => ActionCategory.Quality;
    public override int Level => 84;
    public override uint ActionId => 100411;

    public override int CPCost => Simulation.ActionStates.TouchComboIdx == 2 ? 18 : 46;
    public override float Efficiency => 1.50f;
    public override bool IncreasesQuality => true;
}
