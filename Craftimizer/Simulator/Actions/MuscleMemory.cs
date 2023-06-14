namespace Craftimizer.Simulator.Actions;

internal class MuscleMemory : BaseAction
{
    public MuscleMemory(Simulation simulation) : base(simulation) { }

    public override ActionCategory Category => ActionCategory.FirstTurn;
    public override int Level => 54;
    public override int ActionId => 100379;

    public override int CPCost => 6;
    public override float Efficiency => 3.00f;
    public override bool IncreasesProgress => true;

    public override bool CanUse => Simulation.IsFirstStep && base.CanUse;

    public override void UseSuccess()
    {
        base.UseSuccess();
        Simulation.AddEffect(Effect.MuscleMemory, 5);
    }
}
