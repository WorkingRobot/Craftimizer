namespace Craftimizer.Simulator.Actions;

internal sealed class FinalAppraisal : BaseBuffAction
{
    public override ActionCategory Category => ActionCategory.Synthesis;
    public override int Level => 42;
    public override uint ActionId => 19012;

    public override bool IncreasesStepCount => false;

    public override EffectType Effect => EffectType.FinalAppraisal;
    // This is set to 4 since IncreaseStepCount is false.
    // Usually it adds 1 extra duration and then it would tick it down, but IncreaseStepCount prevents that.
    public override byte Duration => 4;

    public override int CPCost(Simulator s) => 1;
}
