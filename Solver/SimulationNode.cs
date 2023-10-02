using Craftimizer.Simulator;
using Craftimizer.Simulator.Actions;
using System.Runtime.InteropServices;

namespace Craftimizer.Solver;

[StructLayout(LayoutKind.Auto)]
public struct SimulationNode
{
    public readonly SimulationState State;
    public readonly ActionType? Action;
    public readonly CompletionState SimulationCompletionState;

    public ActionSet AvailableActions;

    public readonly CompletionState CompletionState => GetCompletionState(SimulationCompletionState, AvailableActions);

    public readonly bool IsComplete => CompletionState != CompletionState.Incomplete;

    public SimulationNode(SimulationState state, ActionType? action, CompletionState completionState, ActionSet actions)
    {
        State = state;
        Action = action;
        SimulationCompletionState = completionState;
        AvailableActions = actions;
    }

    public static CompletionState GetCompletionState(CompletionState simCompletionState, ActionSet actions) =>
        actions.IsEmpty && simCompletionState == CompletionState.Incomplete ?
        CompletionState.NoMoreActions :
        simCompletionState;

    public readonly float? CalculateScore(MCTSConfig config) =>
        CalculateScoreForState(State, SimulationCompletionState, config);

    private static bool CanByregot(SimulationState state)
    {
        if (state.ActiveEffects.InnerQuiet == 0)
            return false;

        return BaseComboAction.VerifyDurability2(state, 10);
    }

    public static float? CalculateScoreForState(SimulationState state, CompletionState completionState, MCTSConfig config)
    {
        if (completionState != CompletionState.ProgressComplete)
            return null;

        static float Apply(float bonus, float value, float target) =>
            bonus * Math.Min(1f, value / target);

        var progressScore = Apply(
            config.ScoreProgress,
            state.Progress,
            state.Input.Recipe.MaxProgress
        );

        var byregotBonus = CanByregot(state) ? (state.ActiveEffects.InnerQuiet * .2f + 1) * state.Input.BaseQualityGain : 0;
        var qualityScore = Apply(
            config.ScoreQuality,
            state.Quality + byregotBonus,
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
            config.ScoreSteps * (1f - (float)(state.ActionCount + 1) / config.MaxStepCount);

        return progressScore + qualityScore + durabilityScore + cpScore + fewerStepsScore;
    }
}
