namespace Craftimizer.Simulator.Actions;

internal sealed class TrainedFinesse() : BaseAction(
    ActionCategory.Quality, 90, 100435,
    increasesQuality: true,
    durabilityCost: 0,
    defaultCPCost: 32,
    defaultEfficiency: 100
    )
{
    public override bool CouldUse(Simulator s) =>
        s.GetEffectStrength(EffectType.InnerQuiet) == 10
        && base.CouldUse(s);
}
