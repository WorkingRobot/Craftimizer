namespace Craftimizer.Simulator.Actions;

internal sealed class PrudentTouch() : BaseAction(
    ActionCategory.Quality, 66, 100227,
    increasesQuality: true,
    durabilityCost: 5,
    defaultCPCost: 25,
    defaultEfficiency: 100
    )
{
    public override bool CouldUse(Simulator s) =>
        !(s.HasEffect(EffectType.WasteNot) || s.HasEffect(EffectType.WasteNot2))
        && base.CouldUse(s);
}
