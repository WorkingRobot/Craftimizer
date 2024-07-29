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

    public static float? CalculateScoreForState(in SimulationState state, CompletionState completionState, in MCTSConfig config)
    {
        if (completionState != CompletionState.ProgressComplete)
            return null;

        var stepScore = 1f - ((float)(state.ActionCount + 1) / config.MaxStepCount);

        if (state.Input.Recipe.MaxQuality == 0)
            return stepScore;

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        static float Apply(float multiplier, float value, float target) =>
            multiplier * (target > 0 ? Math.Clamp(value / target, 0, 1) : 1);

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        static float ApplyNondominant(float multiplier, float dominance, float value, float target) =>
            Apply(float.Lerp(multiplier, 0, dominance), value, target);

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        static float ApplyNondominant2(float multiplier, float dominance, float score) =>
            float.Lerp(multiplier, 0, dominance) * score;

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        static float ApplyDominant(float multiplier, float dominance, float value, float target) =>
            Apply(float.Lerp(multiplier, 1, dominance), value, target);

        var qualityDominance = state.ActionCount / config.MaxStepCount;

        var progressScore = ApplyNondominant(
            config.ScoreProgress,
            qualityDominance,
            state.Progress,
            state.Input.Recipe.MaxProgress
        );

        var qualityScore = ApplyDominant(
            config.ScoreQuality,
            qualityDominance,
            state.Quality,
            state.Input.Recipe.MaxQuality
        );

        var durabilityScore = ApplyNondominant(
            config.ScoreDurability,
            qualityDominance,
            state.Durability,
            state.Input.Recipe.MaxDurability
        );

        var cpScore = ApplyNondominant(
            config.ScoreCP,
            qualityDominance,
            state.CP,
            state.Input.Stats.CP
        );

        var fewerStepsScore = ApplyNondominant2(config.ScoreSteps, qualityDominance, stepScore);

        return progressScore + qualityScore + durabilityScore + cpScore + fewerStepsScore;
    }
}
