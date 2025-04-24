namespace Craftimizer.Simulator.Actions;

internal sealed class PreparatoryTouch() : BaseAction(
    ActionCategory.Quality, 71, 100299,
    increasesQuality: true,
    durabilityCost: 20,
    defaultCPCost: 40,
    defaultEfficiency: 200)
{
    public override void UseSuccess(RotationSimulator s)
    {
        base.UseSuccess(s);
        s.StrengthenEffect(EffectType.InnerQuiet);
    }
}
