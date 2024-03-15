namespace Craftimizer.Simulator.Actions;

internal sealed class FinalAppraisal : BaseBuffAction
{
    public int CP = 1;

    public FinalAppraisal()
    {
        Category = ActionCategory.Synthesis;
        Level = 42;
        ActionId = 19012;
        Effect = EffectType.FinalAppraisal;
        Duration = 4;
        IncreasesStepCount = false;
    }

    public override void CPCost(Simulator s, ref int cost)
    {
        cost = CP;
    }
}
