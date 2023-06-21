namespace Craftimizer.Simulator.Actions;

internal sealed class HeartAndSoul : BaseBuffAction
{
    public override ActionCategory Category => ActionCategory.Other;
    public override int Level => 86;
    public override uint ActionId => 100419;

    public override int CPCost => 0;
    public override bool IncreasesStepCount => false;

    public override EffectType Effect => EffectType.HeartAndSoul;

    public override bool CanUse => Simulation.Input.Stats.IsSpecialist && !Simulation.ActionStates.UsedHeartAndSoul;
}
