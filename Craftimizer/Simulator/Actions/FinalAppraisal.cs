namespace Craftimizer.Simulator.Actions;

internal class FinalAppraisal : BaseAction
{
    public FinalAppraisal(Simulation simulation) : base(simulation) { }

    public override ActionCategory Category => ActionCategory.Synthesis;
    public override int Level => 42;
    public override int ActionId => 19012;

    public override int CPCost => 1;
    public override int DurabilityCost => 0;
    public override bool IncreasesStepCount => false;

    public override void UseSuccess() =>
        Simulation.AddEffect(Effect.FinalAppraisal, 5);
}
