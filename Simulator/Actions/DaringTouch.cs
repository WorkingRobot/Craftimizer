namespace Craftimizer.Simulator.Actions;

internal sealed class DaringTouch() : BaseAction(
    ActionCategory.Quality, 96, 100451,
    increasesQuality: true,
    defaultCPCost: 0,
    defaultEfficiency: 150,
    defaultSuccessRate: 60
    )
{
    public override bool CouldUse(Simulator s) =>
        s.HasEffect(EffectType.Expedience) && base.CouldUse(s);
}
