using Craftimizer.Simulator;
using Craftimizer.Simulator.Actions;
using Sim = Craftimizer.Simulator.Simulator;

namespace Craftimizer.Solver.Crafty;

public class Simulator : Sim
{
    public new CompletionState CompletionState =>
        (ActionHistory.Count + 1) >= Solver.MaxStepCount ?
        CompletionState.MaxActionCountReached :
        (CompletionState)base.CompletionState;
    public override bool IsComplete => CompletionState != CompletionState.Incomplete;

    public Simulator(SimulationState state) : base(state)
    {
    }

    // Disable randomization
    public override bool RollSuccessRaw(float successRate) => successRate == 1;
    public override void StepCondition() { }

    private static readonly ActionType[] AcceptedActions = new[]
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

    // https://github.com/alostsock/crafty/blob/cffbd0cad8bab3cef9f52a3e3d5da4f5e3781842/crafty/src/craft_state.rs#L137
    public List<ActionType> AvailableActionsHeuristic(bool strict)
    {
        if (IsComplete)
            return new();

        ActionUtils.SetSimulation(this);
        return AcceptedActions.Where(action =>
        {
            var baseAction = action.WithUnsafe();

            if (!baseAction.CanUse)
                return false;

            if (CalculateSuccessRate(baseAction.SuccessRate) != 1)
                return false;

            // don't allow quality moves at max quality
            if (Quality >= Input.Recipe.MaxQuality && baseAction.IncreasesQuality)
                return false;

            if (action == ActionType.Observe &&
                IsPreviousAction(ActionType.Observe))
                return false;

            if (action == ActionType.FinalAppraisal)
                return false;

            if (strict)
            {
                // always used Trained Eye if it's available
                if (action == ActionType.TrainedEye)
                    return true;

                // only allow Focused moves after Observe
                if (IsPreviousAction(ActionType.Observe) &&
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
                    var progress_increase = CalculateProgressGain(baseAction.Efficiency);
                    var would_finish = Progress + progress_increase >= Input.Recipe.MaxProgress;

                    if (would_finish)
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

            return true;
        }).ToList();
    }
}
