namespace Craftimizer.Simulator.Actions;

internal sealed class ByregotsBlessing() : BaseAction(
    ActionCategory.Quality, 50, 100339,
    increasesQuality: true,
    defaultCPCost: 24,
    defaultEfficiency: 100)
{
    public override int Efficiency(Simulator s) =>
        100 + (20 * s.GetEffectStrength(EffectType.InnerQuiet));

    public override bool CouldUse(Simulator s) =>
        s.HasEffect(EffectType.InnerQuiet) && base.CouldUse(s);

    public override void UseSuccess(Simulator s)
    {
        base.UseSuccess(s);
        s.RemoveEffect(EffectType.InnerQuiet);
    }
}
