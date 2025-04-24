namespace Craftimizer.Simulator.Actions;

internal sealed class BasicSynthesis() : BaseAction(
    ActionCategory.Synthesis, 1, 100001,
    increasesProgress: true,
    defaultCPCost: 0,
    defaultEfficiency: 100
    )
{
    // Basic Synthesis Mastery Trait
    public override int Efficiency(RotationSimulator s) =>
        s.Input.Stats.Level >= 31 ? 120 : 100;
}
