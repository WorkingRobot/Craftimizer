namespace Craftimizer.Simulator.Actions;

internal sealed class Reflect : BaseAction
{
    public override ActionCategory Category => ActionCategory.FirstTurn;
    public override int Level => 69;
    public override uint ActionId => 100387;

    public override int CPCost => 6;
    public override float Efficiency => 1.00f;
    public override bool IncreasesQuality => true;

    public override bool CanUse => Simulation.IsFirstStep && base.CanUse;

    public override void UseSuccess()
    {
        base.UseSuccess();
        Simulation.StrengthenEffect(EffectType.InnerQuiet);
    }
}
