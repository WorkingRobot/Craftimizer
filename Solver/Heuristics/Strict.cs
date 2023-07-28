using Craftimizer.Simulator;
using Craftimizer.Simulator.Actions;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;

namespace Craftimizer.Solver.Heuristics;

internal sealed class Strict : IHeuristic
{
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    // https://github.com/alostsock/crafty/blob/cffbd0cad8bab3cef9f52a3e3d5da4f5e3781842/crafty/src/craft_state.rs#L137
    public static bool ShouldUseAction(Simulator s, ActionType action, BaseAction baseAction)
    {
        if (!Normal.ShouldUseAction(s, action, baseAction))
            return false;

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

        // Only allow byregot at 2+ stacks
        if (action == ActionType.ByregotsBlessing &&
            s.GetEffectStrength(EffectType.InnerQuiet) <= 1)
            return false;

        // Don't execute waste not if a type is already active
        if ((action == ActionType.WasteNot || action == ActionType.WasteNot2) &&
            (s.HasEffect(EffectType.WasteNot) || s.HasEffect(EffectType.WasteNot2)))
            return false;

        // Don't observe if you can't combo it into anything
        if (action == ActionType.Observe &&
            s.CP < 12)
            return false;

        // Do not Masters Mend if it would restore less than 25 dur
        if (action == ActionType.MastersMend &&
            s.Input.Recipe.MaxDurability - s.Durability < 25)
            return false;

        // Don't re-execute manipulation/great strides
        if (action == ActionType.Manipulation &&
            s.HasEffect(EffectType.Manipulation))
            return false;

        if (action == ActionType.GreatStrides &&
            s.HasEffect(EffectType.GreatStrides))
            return false;

        // Don't overlap/reapply veneration/innovation if there is 2+ steps left
        if ((action == ActionType.Veneration || action == ActionType.Innovation) &&
            (s.GetEffectDuration(EffectType.Veneration) > 1 || s.GetEffectDuration(EffectType.Innovation) > 1))
            return false;

        return true;
    }

    [Pure]
    public static ActionSet AvailableActions(Simulator s) =>
        IHeuristic.AvailableActions<Strict>(s, Normal.AcceptedActions);
}
