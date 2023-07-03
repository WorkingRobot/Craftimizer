using Craftimizer.Simulator;
using Craftimizer.Simulator.Actions;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;

namespace Craftimizer.Solver.Crafty;

public sealed class Simulator : SimulatorNoRandom
{
    private readonly int maxStepCount;

    public new CompletionState CompletionState => CalculateCompletionState(State, maxStepCount);
    public override bool IsComplete => CompletionState != CompletionState.Incomplete;

    public Simulator(SimulationState state, int maxStepCount) : base(state)
    {
        this.maxStepCount = maxStepCount;
    }

    public static readonly ActionType[] AcceptedActions = new[]
    {
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

    public static readonly int[] AcceptedActionsLUT;

    static Simulator()
    {
        AcceptedActionsLUT = new int[Enum.GetValues<ActionType>().Length];
        for (var i = 0; i < AcceptedActions.Length; i++)
            AcceptedActionsLUT[(byte)AcceptedActions[i]] = i;
    }

    // https://github.com/alostsock/crafty/blob/cffbd0cad8bab3cef9f52a3e3d5da4f5e3781842/crafty/src/craft_state.rs#L146
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    // It's just a bunch of if statements, I would assume this is actually quite simple to follow
#pragma warning disable MA0051 // Method is too long
    private bool CanUseAction(ActionType action, bool strict)
#pragma warning restore MA0051 // Method is too long
    {
        var baseAction = action.Base();

        if (CalculateSuccessRate(baseAction.SuccessRate(this)) != 1)
            return false;

        // don't allow quality moves at max quality
        if (Quality >= Input.Recipe.MaxQuality && baseAction.IncreasesQuality)
            return false;

        if (action == ActionType.Observe &&
            ActionStates.Observed)
            return false;

        if (strict)
        {
            // always used Trained Eye if it's available
            if (action == ActionType.TrainedEye)
                return baseAction.CanUse(this);

            // only allow Focused moves after Observe
            if (ActionStates.Observed &&
                action != ActionType.FocusedSynthesis &&
                action != ActionType.FocusedTouch)
                return false;

            // don't allow quality moves under Muscle Memory for difficult crafts
            if (Input.Recipe.ClassJobLevel == 90 &&
                HasEffect(EffectType.MuscleMemory) &&
                baseAction.IncreasesQuality)
                return false;

            // don't allow pure quality moves under Veneration
            if (HasEffect(EffectType.Veneration) &&
                !baseAction.IncreasesProgress &&
                baseAction.IncreasesQuality)
                return false;

            if (baseAction.IncreasesProgress)
            {
                var progressIncrease = CalculateProgressGain(baseAction.Efficiency(this));
                var wouldFinish = Progress + progressIncrease >= Input.Recipe.MaxProgress;

                if (wouldFinish)
                {
                    // don't allow finishing the craft if there is significant quality remaining
                    if (Quality < (Input.Recipe.MaxQuality / 5))
                        return false;
                }
                else
                {
                    // don't allow pure progress moves under Innovation, if it wouldn't finish the craft
                    if (HasEffect(EffectType.Innovation) &&
                        !baseAction.IncreasesQuality &&
                        baseAction.IncreasesProgress)
                        return false;
                }
            }

            if (action == ActionType.ByregotsBlessing &&
                GetEffectStrength(EffectType.InnerQuiet) <= 1)
                return false;

            if ((action == ActionType.WasteNot || action == ActionType.WasteNot2) &&
                (HasEffect(EffectType.WasteNot) || HasEffect(EffectType.WasteNot2)))
                return false;

            if (action == ActionType.Observe &&
                CP < 5)
                return false;

            if (action == ActionType.MastersMend &&
                Input.Recipe.MaxDurability - Durability < 25)
                return false;

            if (action == ActionType.Manipulation &&
                HasEffect(EffectType.Manipulation))
                return false;

            if (action == ActionType.GreatStrides &&
                HasEffect(EffectType.GreatStrides))
                return false;

            if ((action == ActionType.Veneration || action == ActionType.Innovation) &&
                (GetEffectDuration(EffectType.Veneration) > 1 || GetEffectDuration(EffectType.Innovation) > 1))
                return false;
        }

        return baseAction.CanUse(this);
    }

    // https://github.com/alostsock/crafty/blob/cffbd0cad8bab3cef9f52a3e3d5da4f5e3781842/crafty/src/craft_state.rs#L137
    public ActionSet AvailableActionsHeuristic(bool strict)
    {
        if (IsComplete)
            return new();

        var ret = new ActionSet();
        foreach (var action in AcceptedActions)
            if (CanUseAction(action, strict))
                ret.AddAction(action);
        return ret;
    }

    public static CompletionState CalculateCompletionState(SimulationState state, int maxStepCount) =>
        (state.ActionCount + 1) >= maxStepCount ?
        CompletionState.MaxActionCountReached :
        (CompletionState)CalculateCompletionState(state);
}
