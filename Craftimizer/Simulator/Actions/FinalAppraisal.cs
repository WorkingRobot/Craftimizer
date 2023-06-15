namespace Craftimizer.Simulator.Actions;

internal class FinalAppraisal : BaseBuffAction
{
    public FinalAppraisal(Simulation simulation) : base(simulation) { }

    public override ActionCategory Category => ActionCategory.Synthesis;
    public override int Level => 42;
    public override int ActionId => 19012;

    public override int CPCost => 1;
    public override bool IncreasesStepCount => false;

    public override Effect Effect => new() { Type = EffectType.FinalAppraisal, Duration = 5 };
}
