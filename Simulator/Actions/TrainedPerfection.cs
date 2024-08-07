namespace Craftimizer.Simulator.Actions;

internal sealed class TrainedPerfection() : BaseBuffAction(
    ActionCategory.Durability, 100, 100475,
    EffectType.TrainedPerfection, duration: 1,
    macroWaitTime: 3
    )
{
    public override bool IsPossible(Simulator s) =>
        base.IsPossible(s) && !s.ActionStates.UsedTrainedPerfection;

    public override bool CouldUse(Simulator s) =>
        !s.ActionStates.UsedTrainedPerfection;

    public override string GetTooltip(Simulator s, bool addUsability) =>
        GetBaseTooltip(s, addUsability);
}
