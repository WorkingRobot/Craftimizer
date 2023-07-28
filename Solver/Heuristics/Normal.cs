using Craftimizer.Simulator.Actions;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;

namespace Craftimizer.Solver.Heuristics;

internal sealed class Normal : IHeuristic
{
    public static readonly ActionType[] AcceptedActions = new[]
    {
        ActionType.StandardTouchCombo,
        ActionType.AdvancedTouchCombo,
        ActionType.FocusedTouchCombo,
        ActionType.FocusedSynthesisCombo,
        ActionType.TrainedFinesse,
        ActionType.PrudentSynthesis,
        ActionType.Groundwork,
        ActionType.AdvancedTouch,
        ActionType.CarefulSynthesis,
        ActionType.TrainedEye,
        ActionType.DelicateSynthesis,
        ActionType.PreparatoryTouch,
        ActionType.Reflect,
        ActionType.FocusedTouch,
        ActionType.FocusedSynthesis,
        ActionType.PrudentTouch,
        ActionType.Manipulation,
        ActionType.MuscleMemory,
        ActionType.ByregotsBlessing,
        ActionType.WasteNot2,
        ActionType.BasicSynthesis,
        ActionType.Innovation,
        ActionType.GreatStrides,
        ActionType.StandardTouch,
        ActionType.Veneration,
        ActionType.WasteNot,
        ActionType.Observe,
        ActionType.MastersMend,
        ActionType.BasicTouch,
    };

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    // https://github.com/alostsock/crafty/blob/cffbd0cad8bab3cef9f52a3e3d5da4f5e3781842/crafty/src/craft_state.rs#L137
    public static bool ShouldUseAction(Simulator s, ActionType action, BaseAction baseAction)
    {
        if (s.CalculateSuccessRate(baseAction.SuccessRate(s)) != 1)
            return false;

        // don't allow quality moves at max quality
        if (s.Quality >= s.Input.Recipe.MaxQuality && baseAction.IncreasesQuality)
            return false;

        // Don't allow observe after already observing
        if (action == ActionType.Observe &&
            s.ActionStates.Observed)
            return false;

        return true;
    }

    [Pure]
    public static ActionSet AvailableActions(Simulator s) =>
        IHeuristic.AvailableActions<Normal>(s, AcceptedActions);
}
