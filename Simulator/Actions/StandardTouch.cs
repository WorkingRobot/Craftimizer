namespace Craftimizer.Simulator.Actions;

internal sealed class StandardTouch : BaseAction
{
    public override ActionCategory Category => ActionCategory.Quality;
    public override int Level => 18;
    public override uint ActionId => 100004;

    public override bool IncreasesQuality => true;

    public override int CPCost<S>(Simulator<S> s) => s.ActionStates.TouchComboIdx == 1 ? 18 : 32;
    public override int Efficiency<S>(Simulator<S> s) => 125;
}
