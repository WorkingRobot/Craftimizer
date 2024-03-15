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

    public static float? CalculateScoreForState(in SimulationState state, CompletionState completionState, MCTSConfig config)
    {
        if (completionState != CompletionState.ProgressComplete)
            return null;

        if (state.Input.Recipe.MaxQuality == 0)
            return 1f - ((float)(state.ActionCount + 1) / config.MaxStepCount);

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        static float Apply(float bonus, float value, float target) =>
            bonus * (target > 0 ? Math.Clamp(value / target, 0, 1) : 1);

        var progressScore = Apply(
            config.ScoreProgress,
            state.Progress,
            state.Input.Recipe.MaxProgress
        );

        var qualityScore = Apply(
            config.ScoreQuality,
            state.Quality,
            state.Input.Recipe.MaxQuality
        );

        var durabilityScore = Apply(
            config.ScoreDurability,
            state.Durability,
            state.Input.Recipe.MaxDurability
        );

        var cpScore = Apply(
            config.ScoreCP,
            state.CP,
            state.Input.Stats.CP
        );

        var fewerStepsScore =
            config.ScoreSteps * (1f - ((float)(state.ActionCount + 1) / config.MaxStepCount));

        return progressScore + qualityScore + durabilityScore + cpScore + fewerStepsScore;
    }
}
