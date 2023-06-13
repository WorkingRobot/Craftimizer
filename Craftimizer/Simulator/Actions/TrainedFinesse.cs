namespace Craftimizer.Simulator.Actions;

internal class TrainedFinesse : BaseAction
{
    public TrainedFinesse(Simulation simulation) : base(simulation) { }

    public override ActionCategory Category => ActionCategory.Quality;
    public override int Level => 90;

    public override int CPCost => 32;
    public override float Efficiency => 1.00f;
    public override int DurabilityCost => 0;

    public override bool CanUse =>
        (Simulation.GetEffect(Effect.InnerQuiet)?.Strength ?? 0) == 10
        && base.CanUse;

    public override void UseSuccess() =>
        Simulation.IncreaseProgress(Efficiency);
}
