namespace Craftimizer.Simulator.Actions;

internal sealed class RapidSynthesis() : BaseAction(
    ActionCategory.Synthesis, 9, 100363,
    increasesProgress: true,
    defaultCPCost: 0,
    defaultEfficiency: 250,
    defaultSuccessRate: 50
    )
{
    // Rapid Synthesis Mastery Trait
    public override int Efficiency(Simulator s) =>
        s.Input.Stats.Level >= 63 ? 500 : 250;
}
