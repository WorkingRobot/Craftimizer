using Craftimizer.Simulator;
using Craftimizer.Simulator.Actions;

namespace Craftimizer.Solver.Crafty;

public readonly struct SimulationNode
{
    public readonly SimulationState State;
    public readonly ActionType? Action;
    public readonly CompletionState SimulationCompletionState;
    public readonly NodeData Data;

    public CompletionState CompletionState =>
        Data.AvailableActions.Count == 0 && SimulationCompletionState == CompletionState.Incomplete ?
        CompletionState.NoMoreActions :
        SimulationCompletionState;

    public bool IsComplete => CompletionState != CompletionState.Incomplete;

    public SimulationNode(SimulationState state, ActionType? action, CompletionState completionState, NodeData data)
    {
        State = state;
        Action = action;
        SimulationCompletionState = completionState;
        Data = data;
    }

    public float? CalculateScore()
    {
        if (CompletionState != CompletionState.ProgressComplete)
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
            State.Progress,
            State.Input.Recipe.MaxProgress
        );

        var qualityScore = Apply(
            qualityBonus,
            State.Quality,
            State.Input.Recipe.MaxQuality
        );

        var durabilityScore = Apply(
            durabilityBonus,
            State.Durability,
            State.Input.Recipe.MaxDurability
        );

        var cpScore = Apply(
            cpBonus,
            State.CP,
            State.Input.Stats.CP
        );

        var fewerStepsScore =
            fewerStepsBonus * (1f - ((float)(State.ActionCount + 1) / Solver.MaxStepCount));

        return progressScore + qualityScore + durabilityScore + cpScore + fewerStepsScore;
    }
}
