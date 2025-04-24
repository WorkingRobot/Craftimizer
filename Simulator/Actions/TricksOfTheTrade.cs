namespace Craftimizer.Simulator.Actions;

internal sealed class TricksOfTheTrade() : BaseAction(
    ActionCategory.Other, 13, 100371,
    durabilityCost: 0,
    defaultCPCost: 0
    )
{
    public override bool CouldUse(RotationSimulator s) =>
        (s.Condition is Condition.Good or Condition.Excellent || s.HasEffect(EffectType.HeartAndSoul))
        && base.CouldUse(s);

    public override void UseSuccess(RotationSimulator s)
    {
        s.RestoreCP(20);
        if (s.Condition is not (Condition.Good or Condition.Excellent))
            s.RemoveEffect(EffectType.HeartAndSoul);
    }
}
