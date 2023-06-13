namespace Craftimizer.Simulator.Actions;

internal class TrainedEye : BaseAction
{
    public TrainedEye(Simulation simulation) : base(simulation) { }

    public override ActionCategory Category => ActionCategory.FirstTurn;
    public override int Level => 80;

    public override int CPCost => 250;
    public override float Efficiency => 0f;

    public override bool CanUse => Simulation.IsFirstStep && base.CanUse;

    public override void UseSuccess() =>
        Simulation.IncreaseQuality(float.PositiveInfinity);
}
