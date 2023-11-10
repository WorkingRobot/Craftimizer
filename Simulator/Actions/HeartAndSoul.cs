namespace Craftimizer.Simulator.Actions;

internal sealed class HeartAndSoul : BaseBuffAction
{
    public override ActionCategory Category => ActionCategory.Other;
    public override int Level => 86;
    public override uint ActionId => 100419;
    public override int MacroWaitTime => 3;

    public override bool IncreasesStepCount => false;

    public override EffectType Effect => EffectType.HeartAndSoul;

    public override int CPCost<S>(Simulator<S> s) => 0;

    public override bool CanUse<S>(Simulator<S> s) => s.Input.Stats.IsSpecialist && !s.ActionStates.UsedHeartAndSoul;

    public override string GetTooltip<S>(Simulator<S> s, bool addUsability) =>
        $"{GetBaseTooltip(s, addUsability)}Specialist Only";
}
