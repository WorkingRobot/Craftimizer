namespace Craftimizer.Simulator.Actions;

internal sealed class HeartAndSoul() : BaseBuffAction(
    ActionCategory.Other, 86, 100419,
    EffectType.HeartAndSoul, duration: 1,
    macroWaitTime: 3,
    increasesStepCount: false
    )
{
    public override bool IsPossible(Simulator s) =>
        base.IsPossible(s) && s.Input.Stats.IsSpecialist && !s.ActionStates.UsedHeartAndSoul;

    public override bool CouldUse(Simulator s) =>
        !s.ActionStates.UsedHeartAndSoul;

    public override string GetTooltip(Simulator s, bool addUsability) =>
        $"{GetBaseTooltip(s, addUsability)}Specialist Only\n";
}
