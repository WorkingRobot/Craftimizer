using Craftimizer.Simulator;
using Craftimizer.Simulator.Actions;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;

namespace Craftimizer.Solver;

internal sealed class Simulator : ISimulator
{
    private Simulator() { }

    public static CompletionState GetCompletionState<S>(Simulator<S> s) where S : ISimulator
    {
        var b = ISimulator.GetCompletionStateBase(s);
        if (s.Input.SolverData is SolverConfig { MaxStepCount: var stepCount })
        {
            if (b == CompletionState.Incomplete && (s.ActionCount + 1) >= stepCount)
                return CompletionState.MaxActionCountReached;
        }
        return b;
    }

    public static Condition GetNextRandomCondition<S>(Simulator<S> s) where S : ISimulator =>
        Condition.Normal;

    public static bool RollSuccessRaw<S>(Simulator<S> s, float successRate) where S : ISimulator =>
        successRate == 1;

    // https://github.com/alostsock/crafty/blob/cffbd0cad8bab3cef9f52a3e3d5da4f5e3781842/crafty/src/craft_state.rs#L146
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    // It's just a bunch of if statements, I would assume this is actually quite simple to follow
#pragma warning disable MA0051 // Method is too long
    private static bool CanUseAction(Simulator<Simulator> s, ActionType action, bool strict)
#pragma warning restore MA0051 // Method is too long
    {
        var baseAction = action.Base();

        if (s.CalculateSuccessRate(baseAction.SuccessRate(s)) != 1)
            return false;

        // don't allow quality moves at max quality
        if (s.Quality >= s.Input.Recipe.MaxQuality && baseAction.IncreasesQuality)
            return false;

        if (action == ActionType.Observe &&
            s.ActionStates.Observed)
            return false;

        if (strict)
        {
            // always use Trained Eye if it's available
            if (action == ActionType.TrainedEye)
                return baseAction.CanUse(s);

            // only allow Focused moves after Observe
            if (s.ActionStates.Observed &&
                action != ActionType.FocusedSynthesis &&
                action != ActionType.FocusedTouch)
                return false;

            // don't allow quality moves under Muscle Memory for difficult crafts
            if (s.Input.Recipe.ClassJobLevel == 90 &&
                s.HasEffect(EffectType.MuscleMemory) &&
                baseAction.IncreasesQuality)
                return false;

            // use First Turn actions if it's available and the craft is difficult
            if (s.IsFirstStep &&
                s.Input.Recipe.ClassJobLevel == 90 &&
                baseAction.Category != ActionCategory.FirstTurn &&
                s.CP > 10)
                return false;

            // don't allow combo actions if the combo is already in progress
            if (s.ActionStates.TouchComboIdx != 0 &&
                (action == ActionType.StandardTouchCombo || action == ActionType.AdvancedTouchCombo))
                return false;

            // don't allow pure quality moves under Veneration
            if (s.HasEffect(EffectType.Veneration) &&
                !baseAction.IncreasesProgress &&
                baseAction.IncreasesQuality)
                return false;

            // don't allow pure quality moves when it won't be able to finish the craft
            if (baseAction.IncreasesQuality &&
                s.CalculateDurabilityCost(baseAction.DurabilityCost) > s.Durability)
                return false;

            if (baseAction.IncreasesProgress)
            {
                var progressIncrease = s.CalculateProgressGain(baseAction.Efficiency(s));
                var wouldFinish = s.Progress + progressIncrease >= s.Input.Recipe.MaxProgress;

                if (wouldFinish)
                {
                    // don't allow finishing the craft if there is significant quality remaining
                    if (s.Quality < s.Input.Recipe.MaxQuality / 5)
                        return false;
                }
                else
                {
                    // don't allow pure progress moves under Innovation, if it wouldn't finish the craft
                    if (s.HasEffect(EffectType.Innovation) &&
                        !baseAction.IncreasesQuality &&
                        baseAction.IncreasesProgress)
                        return false;
                }
            }

            if (action == ActionType.ByregotsBlessing &&
                s.GetEffectStrength(EffectType.InnerQuiet) <= 1)
                return false;

            if ((action == ActionType.WasteNot || action == ActionType.WasteNot2) &&
                (s.HasEffect(EffectType.WasteNot) || s.HasEffect(EffectType.WasteNot2)))
                return false;

            if (action == ActionType.Observe &&
                s.CP < 12)
                return false;

            if (action == ActionType.MastersMend &&
                s.Input.Recipe.MaxDurability - s.Durability < 25)
                return false;

            if (action == ActionType.Manipulation &&
                s.HasEffect(EffectType.Manipulation))
                return false;

            if (action == ActionType.GreatStrides &&
                s.HasEffect(EffectType.GreatStrides))
                return false;

            if ((action == ActionType.Veneration || action == ActionType.Innovation) &&
                (s.GetEffectDuration(EffectType.Veneration) > 1 || s.GetEffectDuration(EffectType.Innovation) > 1))
                return false;
        }

        return baseAction.CanUse(s);
    }

    // https://github.com/alostsock/crafty/blob/cffbd0cad8bab3cef9f52a3e3d5da4f5e3781842/crafty/src/craft_state.rs#L137
    public static ActionSet AvailableActionsHeuristic(Simulator<Simulator> s, bool strict)
    {
        if (s.IsComplete)
            return new();

        var ret = new ActionSet();
        foreach (var action in ActionSet.AcceptedActions)
            if (CanUseAction(s, action, strict))
                ret.AddAction(action);
        return ret;
    }
}
