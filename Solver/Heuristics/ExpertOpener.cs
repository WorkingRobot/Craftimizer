using Craftimizer.Simulator;
using Craftimizer.Simulator.Actions;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;

namespace Craftimizer.Solver.Heuristics;

internal sealed class ExpertOpener : IHeuristic
{
    public static readonly ActionType[] AcceptedActions = new[]
    {
        ActionType.CarefulObservation,
        ActionType.FinalAppraisal,
        ActionType.Manipulation,
        ActionType.MuscleMemory,
        ActionType.PreciseTouch,
        ActionType.RapidSynthesis,
        ActionType.TricksOfTheTrade,
        ActionType.Veneration,
        ActionType.HeartAndSoul,
    };

    private static readonly BaseAction RapidSynthesis = ActionType.RapidSynthesis.Base();

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ShouldUseAction(Simulator s, ActionType action, BaseAction baseAction)
    {
        // Make sure the first step is muscle memory
        if (baseAction.IncreasesStepCount && s.IsFirstStep && action != ActionType.MuscleMemory)
            return false;

        // Roll for a malleable or sturdy before taking the first muscle memory step
        if (s.Condition is not (Condition.Malleable or Condition.Sturdy) && s.IsFirstStep && action == ActionType.MuscleMemory && s.ActionStates.CarefulObservationCount != 3)
            return false;

        if (baseAction.IncreasesProgress)
        {
            var progressIncrease = s.CalculateProgressGain(baseAction.Efficiency(s));
            var wouldFinish = s.Progress + progressIncrease >= s.Input.Recipe.MaxProgress;

            if (wouldFinish)
                return false;
        }

        if (action == ActionType.FinalAppraisal)
        {
            if (s.HasEffect(EffectType.FinalAppraisal))
                return false;

            var rapidProgressIncrease = s.CalculateProgressGain(RapidSynthesis.Efficiency(s));
            var wouldRapidFinish = s.Progress + rapidProgressIncrease >= s.Input.Recipe.MaxProgress;
            return wouldRapidFinish;
        }

        // Don't reapply manipulation if there is 2+ steps left
        if (action == ActionType.Manipulation &&
            s.GetEffectDuration(EffectType.Manipulation) > 1)
            return false;

        // Don't reapply veneration if there is 2+ steps left
        if (action == ActionType.Veneration &&
            s.GetEffectDuration(EffectType.Veneration) > 1)
            return false;

        return s.Condition switch
        {
            Condition.Centered or Condition.Sturdy or Condition.Normal or Condition.GoodOmen =>
                action is ActionType.RapidSynthesis or ActionType.Veneration ||
                action is ActionType.Manipulation && !s.HasEffect(EffectType.MuscleMemory),
            Condition.Malleable =>
                action is ActionType.RapidSynthesis,
            Condition.Primed or Condition.Pliant =>
                action is ActionType.RapidSynthesis or ActionType.Manipulation or ActionType.Veneration,
            Condition.Good =>
                action is ActionType.RapidSynthesis or ActionType.TricksOfTheTrade or ActionType.PreciseTouch,
            _ => true
        };
    }

    [Pure]
    public static ActionSet AvailableActions(Simulator s) =>
        IHeuristic.AvailableActions<Strict>(s, AcceptedActions);
}
