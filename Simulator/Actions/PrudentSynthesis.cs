namespace Craftimizer.Simulator.Actions;

internal sealed class PrudentSynthesis() : BaseAction(
    ActionCategory.Synthesis, 88, 100427,
    increasesProgress: true,
    durabilityCost: 5,
    defaultCPCost: 18,
    defaultEfficiency: 180
    )
{
    public override bool CouldUse(RotationSimulator s) =>
        !(s.HasEffect(EffectType.WasteNot) || s.HasEffect(EffectType.WasteNot2))
        && base.CouldUse(s);
}
