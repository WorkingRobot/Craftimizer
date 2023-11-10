namespace Craftimizer.Simulator.Actions;

internal sealed class ByregotsBlessing : BaseAction
{
    public override ActionCategory Category => ActionCategory.Quality;
    public override int Level => 50;
    public override uint ActionId => 100339;

    public override bool IncreasesQuality => true;

    public override int CPCost<S>(Simulator<S> s) => 24;
    public override int Efficiency<S>(Simulator<S> s) => 100 + (20 * s.GetEffectStrength(EffectType.InnerQuiet));

    public override bool CanUse<S>(Simulator<S> s) => s.HasEffect(EffectType.InnerQuiet) && base.CanUse(s);

    public override void UseSuccess<S>(Simulator<S> s)
    {
        base.UseSuccess(s);
        s.RemoveEffect(EffectType.InnerQuiet);
    }
}
