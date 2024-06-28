namespace Craftimizer.Simulator.Actions;

internal sealed class DelicateSynthesis() : BaseAction(
    ActionCategory.Synthesis, 76, 100323,
    increasesProgress: true, increasesQuality: true,
    defaultCPCost: 32,
    defaultEfficiency: 100
    )
{
    public override void UseSuccess(Simulator s)
    {
        // Delicate Synthesis Mastery Trait
        var hasTrait = s.Input.Stats.Level >= 94;
        s.IncreaseProgress(hasTrait ? 150 : 100);
        s.IncreaseQuality(100);
    }
}
