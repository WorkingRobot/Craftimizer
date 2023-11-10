namespace Craftimizer.Simulator.Actions;

internal sealed class AdvancedTouch : BaseAction
{
    public override ActionCategory Category => ActionCategory.Quality;
    public override int Level => 84;
    public override uint ActionId => 100411;

    public override bool IncreasesQuality => true;

    public override int CPCost<S>(Simulator<S> s) => s.ActionStates.TouchComboIdx == 2 ? 18 : 46;
    public override int Efficiency<S>(Simulator<S> s) => 150;
}
