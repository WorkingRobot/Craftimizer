namespace Craftimizer.Simulator.Actions;

internal sealed class FinalAppraisal : BaseBuffAction
{
    public override ActionCategory Category => ActionCategory.Synthesis;
    public override int Level => 42;
    public override uint ActionId => 19012;

    public override int CPCost => 1;
    public override bool IncreasesStepCount => false;

    public override EffectType Effect => EffectType.FinalAppraisal;
    public override byte Duration => 5;
}
