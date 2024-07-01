namespace Craftimizer.Simulator.Actions;

internal sealed class FinalAppraisal() : BaseBuffAction(
    ActionCategory.Other, 42, 19012,
    EffectType.FinalAppraisal, duration: 5,
    increasesStepCount: false,
    defaultCPCost: 1)
{

}
