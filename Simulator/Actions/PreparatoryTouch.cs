namespace Craftimizer.Simulator.Actions;

internal sealed class PreparatoryTouch : BaseAction
{
    public override ActionCategory Category => ActionCategory.Quality;
    public override int Level => 71;
    public override uint ActionId => 100299;

    public override bool IncreasesQuality => true;
    public override int DurabilityCost => 20;

    public override int CPCost(Simulator s) => 40;
    public override int Efficiency(Simulator s) => 200;

    public override void UseSuccess(Simulator s)
    {
        base.UseSuccess(s);
        s.StrengthenEffect(EffectType.InnerQuiet);
    }
}
