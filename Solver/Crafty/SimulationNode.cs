using Craftimizer.Simulator;
using Craftimizer.Simulator.Actions;

namespace Craftimizer.Solver.Crafty;

public struct SimulationNode
{
    public readonly SimulationState State;
    public readonly ActionType? Action;
    public readonly CompletionState SimulationCompletionState;

    public ActionSet AvailableActions;
    public NodeScores Scores;

    public CompletionState CompletionState => GetCompletionState(SimulationCompletionState, AvailableActions);

    public bool IsComplete => CompletionState != CompletionState.Incomplete;

    public SimulationNode(SimulationState state, ActionType? action, CompletionState completionState, ActionSet actions)
    {
        State = state;
        Action = action;
        SimulationCompletionState = completionState;
        AvailableActions = actions;
    }

    public static CompletionState GetCompletionState(CompletionState simCompletionState, ActionSet actions) =>
        actions.Count == 0 && simCompletionState == CompletionState.Incomplete ?
        CompletionState.NoMoreActions :
        simCompletionState;

    public float? CalculateScore() => CalculateScoreForState(State, SimulationCompletionState);

    public static float? CalculateScoreForState(SimulationState state, CompletionState completionState)
    {
        if (completionState != CompletionState.ProgressComplete)
            return null;

        static float Apply(float bonus, float value, float target) =>
            bonus * Math.Min(1f, value / target);

        var progressBonus = 0.20f;
        var qualityBonus = 0.65f;
        var durabilityBonus = 0.05f;
        var cpBonus = 0.05f;
        var fewerStepsBonus = 0.05f;

        var progressScore = Apply(
            progressBonus,
            state.Progress,
            state.Input.Recipe.MaxProgress
        );

        var qualityScore = Apply(
            qualityBonus,
            state.Quality,
            state.Input.Recipe.MaxQuality
        );

        var durabilityScore = Apply(
            durabilityBonus,
            state.Durability,
            state.Input.Recipe.MaxDurability
        );

        var cpScore = Apply(
            cpBonus,
            state.CP,
            state.Input.Stats.CP
        );

        var fewerStepsScore =
            fewerStepsBonus * (1f - ((float)(state.ActionCount + 1) / Solver.MaxStepCount));

        return progressScore + qualityScore + durabilityScore + cpScore + fewerStepsScore;
    }
}
