namespace Craftimizer.Simulator.Actions;

internal sealed class PreparatoryTouch : BaseAction
{
    public override ActionCategory Category => ActionCategory.Quality;
    public override int Level => 71;
    public override uint ActionId => 100299;

    public override bool IncreasesQuality => true;
    public override int DurabilityCost => 20;

    public override int CPCost<S>(Simulator<S> s) => 40;
    public override int Efficiency<S>(Simulator<S> s) => 200;

    public override void UseSuccess<S>(Simulator<S> s)
    {
        base.UseSuccess(s);
        s.StrengthenEffect(EffectType.InnerQuiet);
    }
}
