namespace Craftimizer.Simulator.Actions;

internal sealed class IntensiveSynthesis() : BaseAction(
    ActionCategory.Synthesis, 78, 100315,
    increasesProgress: true,
    defaultCPCost: 6,
    defaultEfficiency: 400
    )
{
    public override bool CouldUse(Simulator s) =>
        (s.Condition is Condition.Good or Condition.Excellent || s.HasEffect(EffectType.HeartAndSoul))
        && base.CouldUse(s);

    public override void UseSuccess(Simulator s)
    {
        base.UseSuccess(s);
        if (s.Condition is not (Condition.Good or Condition.Excellent))
            s.RemoveEffect(EffectType.HeartAndSoul);
    }
}
