using Craftimizer.Simulator;
using Craftimizer.Simulator.Actions;
using System.Runtime.InteropServices;

namespace Craftimizer.Solver.Crafty;

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

    public readonly float? CalculateScore(int maxStepCount) => CalculateScoreForState(State, SimulationCompletionState, maxStepCount);

    private static bool CanByregot(SimulationState state)
    {
        if (state.ActiveEffects.InnerQuiet == 0)
            return false;

        var wasteNot = Math.Max(state.ActiveEffects.WasteNot, state.ActiveEffects.WasteNot2);
        var manipulation = state.ActiveEffects.Manipulation;
        var durability = state.Durability;
        durability -= wasteNot-- > 0 ? 5 : 10;
        if (durability <= 0)
            return false;
        if (manipulation-- > 0)
            durability += 5;
        durability -= wasteNot-- > 0 ? 5 : 10;

        return durability >= 0;
    }

    public static float? CalculateScoreForState(SimulationState state, CompletionState completionState, int maxStepCount)
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

        var byregotBonus = CanByregot(state) ? (state.ActiveEffects.InnerQuiet * .2f + 1) * state.Input.BaseQualityGain : 0;
        var quality = Math.Clamp(state.Quality + byregotBonus, 0, state.Input.Recipe.MaxQuality);
        var qualityScore = Apply(
            qualityBonus,
            quality,
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
            fewerStepsBonus * (1f - ((float)(state.ActionCount + 1) / maxStepCount));

        return progressScore + qualityScore + durabilityScore + cpScore + fewerStepsScore;
    }
}
