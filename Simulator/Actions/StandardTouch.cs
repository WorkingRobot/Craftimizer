namespace Craftimizer.Simulator.Actions;

internal sealed class StandardTouch() : BaseAction(
    ActionCategory.Quality, 18, 100004,
    increasesQuality: true,
    defaultCPCost: 32,
    defaultEfficiency: 125
    )
{
    public override int CPCost(Simulator s) =>
        s.ActionStates.Combo == ActionProc.UsedBasicTouch ? 18 : 32;
}
