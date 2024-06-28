namespace Craftimizer.Simulator.Actions;

internal sealed class RefinedTouch() : BaseAction(
    ActionCategory.Quality, 92, 100443,
    increasesQuality: true,
    defaultCPCost: 24,
    defaultEfficiency: 100
    )
{
    public override void UseSuccess(Simulator s)
    {
        base.UseSuccess(s);
        if (s.ActionStates.TouchComboIdx == 1)
            s.StrengthenEffect(EffectType.InnerQuiet);
    }
}
