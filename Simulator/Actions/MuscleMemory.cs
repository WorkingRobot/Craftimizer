namespace Craftimizer.Simulator.Actions;

internal sealed class MuscleMemory : BaseAction
{
    public override ActionCategory Category => ActionCategory.FirstTurn;
    public override int Level => 54;
    public override uint ActionId => 100379;

    public override bool IncreasesProgress => true;

    public override int CPCost<S>(Simulator<S> s) => 6;
    public override int Efficiency<S>(Simulator<S> s) => 300;

    public override bool CanUse<S>(Simulator<S> s) => s.IsFirstStep && base.CanUse(s);

    public override void UseSuccess<S>(Simulator<S> s)
    {
        base.UseSuccess(s);
        s.AddEffect(EffectType.MuscleMemory, 5);
    }
}
