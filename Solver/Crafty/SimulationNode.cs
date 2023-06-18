using Craftimizer.Simulator;
using Craftimizer.Simulator.Actions;

namespace Craftimizer.Solver.Crafty;

public readonly record struct SimulationNode
{
    public SimulationState State { get; init; }
    public ActionType? Action { get; init; }
    public List<ActionType> AvailableActions { get; init; }
    public CompletionState SimulationCompletionState { get; init; }
    public CompletionState CompletionState =>
        AvailableActions.Count == 0 && SimulationCompletionState == CompletionState.Incomplete ?
        CompletionState.NoMoreActions :
        SimulationCompletionState;

    public NodeScores Scores { get; init; }

    public bool IsComplete => CompletionState != CompletionState.Incomplete;

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
