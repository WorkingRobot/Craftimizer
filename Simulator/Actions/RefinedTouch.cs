namespace Craftimizer.Simulator.Actions;

internal sealed class RefinedTouch() : BaseAction(
    ActionCategory.Quality, 92, 100443,
    increasesQuality: true,
    defaultCPCost: 24,
    defaultEfficiency: 100
    )
{
    public override void UseSuccess(RotationSimulator s)
    {
        base.UseSuccess(s);
        if (s.ActionStates.Combo == ActionProc.UsedBasicTouch)
            s.StrengthenEffect(EffectType.InnerQuiet);
    }
}
