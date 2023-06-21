namespace Craftimizer.Simulator.Actions;

internal sealed class MuscleMemory : BaseAction
{
    public override ActionCategory Category => ActionCategory.FirstTurn;
    public override int Level => 54;
    public override uint ActionId => 100379;

    public override bool IncreasesProgress => true;

    public override int CPCost(Simulator s) => 6;
    public override float Efficiency(Simulator s) => 3.00f;

    public override bool CanUse(Simulator s) => s.IsFirstStep && base.CanUse(s);

    public override void UseSuccess(Simulator s)
    {
        base.UseSuccess(s);
        s.AddEffect(EffectType.MuscleMemory, 5);
    }
}
