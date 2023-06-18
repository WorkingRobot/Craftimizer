namespace Craftimizer.Simulator.Actions;

internal class ByregotsBlessing : BaseAction
{
    public override ActionCategory Category => ActionCategory.Quality;
    public override int Level => 50;
    public override uint ActionId => 100339;

    public override int CPCost => 24;
    public override float Efficiency => 1.00f + (0.20f * Simulation.GetEffectStrength(EffectType.InnerQuiet));
    public override bool IncreasesQuality => true;

    public override bool CanUse => Simulation.HasEffect(EffectType.InnerQuiet) && base.CanUse;

    public override void UseSuccess()
    {
        base.UseSuccess();
        Simulation.RemoveEffect(EffectType.InnerQuiet);
    }
}
