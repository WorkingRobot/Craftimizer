namespace Craftimizer.Simulator.Actions;

internal sealed class HeartAndSoul() : BaseBuffAction(
    ActionCategory.Other, 86, 100419,
    EffectType.HeartAndSoul, duration: 1,
    macroWaitTime: 3,
    increasesStepCount: false
    )
{
    public override bool IsPossible(RotationSimulator s) =>
        base.IsPossible(s) && s.Input.Stats.IsSpecialist && !s.ActionStates.UsedHeartAndSoul;

    public override bool CouldUse(RotationSimulator s) =>
        !s.ActionStates.UsedHeartAndSoul;

    public override string GetTooltip(RotationSimulator s, bool addUsability) =>
        $"{GetBaseTooltip(s, addUsability)}Specialist Only\n";
}
