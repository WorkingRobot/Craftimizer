namespace Craftimizer.Simulator.Actions;

internal sealed class RapidSynthesis : BaseAction
{
    public override ActionCategory Category => ActionCategory.Synthesis;
    public override int Level => 9;
    public override uint ActionId => 100363;

    public override bool IncreasesProgress => true;

    public override int CPCost<S>(Simulator<S> s) => 0;
    // Rapid Synthesis Mastery Trait
    public override int Efficiency<S>(Simulator<S> s) => s.Input.Stats.Level >= 63 ? 500 : 250;
    public override float SuccessRate<S>(Simulator<S> s) => 0.50f;
}
