namespace Craftimizer.Simulator.Actions;

internal sealed class MuscleMemory() : BaseAction(
    ActionCategory.FirstTurn, 54, 100379,
    increasesProgress: true,
    defaultCPCost: 6,
    defaultEfficiency: 300
    )
{
    public override bool IsPossible(Simulator s) => s.IsFirstStep && base.IsPossible(s);

    public override bool CouldUse(Simulator s) => s.IsFirstStep && base.CouldUse(s);

    public override void UseSuccess(Simulator s)
    {
        base.UseSuccess(s);
        s.AddEffect(EffectType.MuscleMemory, 5 + 1);
    }
}
