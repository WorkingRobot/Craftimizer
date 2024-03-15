namespace Craftimizer.Simulator.Actions;

internal sealed class MuscleMemory : BaseAction
{
    public override ActionCategory Category => ActionCategory.FirstTurn;
    public override int Level => 54;
    public override uint ActionId => 100379;

    public override bool IncreasesProgress => true;

    public override int CPCost(Simulator s) => 6;
    public override int Efficiency(Simulator s) => 300;

    public override bool IsPossible(Simulator s) => s.IsFirstStep && base.IsPossible(s);

    public override bool CouldUse(Simulator s) => s.IsFirstStep && base.CouldUse(s);

    public override void UseSuccess(Simulator s)
    {
        base.UseSuccess(s);
        s.AddEffect(EffectType.MuscleMemory, 5);
    }
}
