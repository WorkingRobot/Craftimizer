namespace Craftimizer.Simulator.Actions;

internal sealed class CarefulSynthesis() : BaseAction(
    ActionCategory.Synthesis, 62, 100203,
    increasesProgress: true,
    defaultCPCost: 7,
    defaultEfficiency: 150
    )
{
    // Careful Synthesis Mastery Trait
    public override int Efficiency(RotationSimulator s) =>
        s.Input.Stats.Level >= 82 ? 180 : 150;
}
