namespace Craftimizer.Simulator.Actions;

internal sealed class HeartAndSoul : BaseBuffAction
{
    public override ActionCategory Category => ActionCategory.Other;
    public override int Level => 86;
    public override uint ActionId => 100419;

    public override bool IncreasesStepCount => false;

    public override EffectType Effect => EffectType.HeartAndSoul;

    public override int CPCost(Simulator s) => 0;

    public override bool CanUse(Simulator s) => s.Input.Stats.IsSpecialist && !s.ActionStates.UsedHeartAndSoul;
}
