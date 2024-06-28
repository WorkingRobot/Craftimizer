namespace Craftimizer.Simulator.Actions;

internal sealed class TrainedPerfection() : BaseBuffAction(
    ActionCategory.Durability, 100, 100475,
    EffectType.TrainedPerfection, duration: 1
    )
{
    public override bool IsPossible(Simulator s) =>
        base.IsPossible(s) && !s.ActionStates.UsedTrainedPerfection;

    public override bool CouldUse(Simulator s) =>
        !s.ActionStates.UsedTrainedPerfection;
}
