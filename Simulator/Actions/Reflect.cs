namespace Craftimizer.Simulator.Actions;

internal sealed class Reflect : BaseAction
{
    public override ActionCategory Category => ActionCategory.FirstTurn;
    public override int Level => 69;
    public override uint ActionId => 100387;

    public override bool IncreasesQuality => true;

    public override int CPCost<S>(Simulator<S> s) => 6;
    public override int Efficiency<S>(Simulator<S> s) => 100;

    public override bool CanUse<S>(Simulator<S> s) => s.IsFirstStep && base.CanUse(s);

    public override void UseSuccess<S>(Simulator<S> s)
    {
        base.UseSuccess(s);
        s.StrengthenEffect(EffectType.InnerQuiet);
    }
}
