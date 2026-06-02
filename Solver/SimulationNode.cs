using Craftimizer.Simulator;
using Craftimizer.Simulator.Actions;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Craftimizer.Solver;

[StructLayout(LayoutKind.Auto)]
public struct SimulationNode(in SimulationState state, ActionType? action, CompletionState completionState, ActionSet actions)
{
    public readonly SimulationState State = state;
    public readonly ActionType? Action = action;
    public readonly CompletionState SimulationCompletionState = completionState;

    public ActionSet AvailableActions = actions;

    public readonly CompletionState CompletionState => GetCompletionState(SimulationCompletionState, AvailableActions);

    public readonly bool IsComplete => CompletionState != CompletionState.Incomplete;

    public static CompletionState GetCompletionState(CompletionState simCompletionState, ActionSet actions) =>
        actions.IsEmpty && simCompletionState == CompletionState.Incomplete ?
        CompletionState.NoMoreActions :
        simCompletionState;

    public readonly float? CalculateScore(in MCTSConfig config) =>
        CalculateScoreForState(State, SimulationCompletionState, config);

    // A tiny constant so any completed craft scores strictly > 0 (preserves MCTS.Search's
    // "MaxScore == 0 => no finish found yet" logic, and avoids a 0 score when stepBonus is 0).
    private const float CompletionBase = 0.01f;

    public static float? CalculateScoreForState(in SimulationState state, CompletionState completionState, in MCTSConfig config)
    {
        // Strictly lexicographic objective, mirroring Raphael:
        //   completion  >  quality (up to target)  >  fewer steps.
        // Only completed crafts are scored (the gate enforces "synthesis finished" as top priority).
        // Durability and CP are deliberately NOT in the objective (they are feasibility/search
        // currency, not goals) — rewarding leftover dur/CP pads the end of a craft (issues #6/#44).
        if (completionState != CompletionState.ProgressComplete)
            return null;

        var stepBonus = 1f - ((float)(state.ActionCount + 1) / config.MaxStepCount);

        var target = config.QualityTarget > 0
            ? Math.Min(config.QualityTarget, state.Input.Recipe.MaxQuality)
            : state.Input.Recipe.MaxQuality;

        // No-quality recipe (or zero target): the only objective is fewer steps.
        if (target <= 0)
            return CompletionBase + ((1f - CompletionBase) * stepBonus);

        var qualityFrac = Math.Clamp((float)state.Quality / target, 0f, 1f);

        // stepWeight is set just below the value of a single quality point, so quality strictly
        // dominates: no quality is ever traded for fewer steps; steps only break (near-)ties.
        var remaining = 1f - CompletionBase;
        var stepWeight = remaining / (target + 1);
        var qualityWeight = remaining - stepWeight;

        return CompletionBase + (qualityWeight * qualityFrac) + (stepWeight * stepBonus);
    }
}
