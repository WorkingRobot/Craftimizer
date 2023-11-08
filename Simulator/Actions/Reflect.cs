namespace Craftimizer.Simulator.Actions;

internal sealed class Reflect : BaseAction
{
    public override ActionCategory Category => ActionCategory.FirstTurn;
    public override int Level => 69;
    public override uint ActionId => 100387;

    public override bool IncreasesQuality => true;

    public override int CPCost(Simulator s) => 6;
    public override int Efficiency(Simulator s) => 100;

    public override bool CanUse(Simulator s) => s.IsFirstStep && base.CanUse(s);

    public override void UseSuccess(Simulator s)
    {
        base.UseSuccess(s);
        s.StrengthenEffect(EffectType.InnerQuiet);
    }
}
