using Craftimizer.Simulator.Actions;
using Action = Raphael.Action;

namespace Craftimizer.Solver;

internal static unsafe class RaphaelUtils
{
    public static ActionType[] ConvertRawActions(IReadOnlyList<Action> actions)
    {
        var result = new ActionType[actions.Count];
        for (var i = 0; i < actions.Count; i++)
            result[i] = ConvertRawAction(actions[i]);
        return result;
    }

    public static Action[] ConvertToRawActions(IReadOnlyList<ActionType> actions)
    {
        var result = new List<Action>(actions.Count);
        foreach(var action in actions)
        {
            if (ConvertToRawAction(action) is { } a)
                result.Add(a);
        }
        return [.. result];
    }

    public static ActionType ConvertRawAction(Action action)
    {
        return action switch
        {
            Action.BasicSynthesis => ActionType.BasicSynthesis,
            Action.BasicTouch => ActionType.BasicTouch,
            Action.MasterMend => ActionType.MastersMend,
            Action.Observe => ActionType.Observe,
            Action.TricksOfTheTrade => ActionType.TricksOfTheTrade,
            Action.WasteNot => ActionType.WasteNot,
            Action.Veneration => ActionType.Veneration,
            Action.StandardTouch => ActionType.StandardTouch,
            Action.GreatStrides => ActionType.GreatStrides,
            Action.Innovation => ActionType.Innovation,
            Action.WasteNot2 => ActionType.WasteNot2,
            Action.ByregotsBlessing => ActionType.ByregotsBlessing,
            Action.PreciseTouch => ActionType.PreciseTouch,
            Action.MuscleMemory => ActionType.MuscleMemory,
            Action.CarefulSynthesis => ActionType.CarefulSynthesis,
            Action.Manipulation => ActionType.Manipulation,
            Action.PrudentTouch => ActionType.PrudentTouch,
            Action.AdvancedTouch => ActionType.AdvancedTouch,
            Action.Reflect => ActionType.Reflect,
            Action.PreparatoryTouch => ActionType.PreparatoryTouch,
            Action.Groundwork => ActionType.Groundwork,
            Action.DelicateSynthesis => ActionType.DelicateSynthesis,
            Action.IntensiveSynthesis => ActionType.IntensiveSynthesis,
            Action.TrainedEye => ActionType.TrainedEye,
            Action.HeartAndSoul => ActionType.HeartAndSoul,
            Action.PrudentSynthesis => ActionType.PrudentSynthesis,
            Action.TrainedFinesse => ActionType.TrainedFinesse,
            Action.RefinedTouch => ActionType.RefinedTouch,
            Action.QuickInnovation => ActionType.QuickInnovation,
            Action.ImmaculateMend => ActionType.ImmaculateMend,
            Action.TrainedPerfection => ActionType.TrainedPerfection,
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, $"Invalid action value {action}"),
        };
    }

    public static Action? ConvertToRawAction(ActionType action)
    {
        return action switch
        {
            ActionType.BasicSynthesis => Action.BasicSynthesis,
            ActionType.BasicTouch => Action.BasicTouch,
            ActionType.MastersMend => Action.MasterMend,
            ActionType.Observe => Action.Observe,
            ActionType.TricksOfTheTrade => Action.TricksOfTheTrade,
            ActionType.WasteNot => Action.WasteNot,
            ActionType.Veneration => Action.Veneration,
            ActionType.StandardTouch => Action.StandardTouch,
            ActionType.GreatStrides => Action.GreatStrides,
            ActionType.Innovation => Action.Innovation,
            ActionType.WasteNot2 => Action.WasteNot2,
            ActionType.ByregotsBlessing => Action.ByregotsBlessing,
            ActionType.PreciseTouch => Action.PreciseTouch,
            ActionType.MuscleMemory => Action.MuscleMemory,
            ActionType.CarefulSynthesis => Action.CarefulSynthesis,
            ActionType.Manipulation => Action.Manipulation,
            ActionType.PrudentTouch => Action.PrudentTouch,
            ActionType.AdvancedTouch => Action.AdvancedTouch,
            ActionType.Reflect => Action.Reflect,
            ActionType.PreparatoryTouch => Action.PreparatoryTouch,
            ActionType.Groundwork => Action.Groundwork,
            ActionType.DelicateSynthesis => Action.DelicateSynthesis,
            ActionType.IntensiveSynthesis => Action.IntensiveSynthesis,
            ActionType.TrainedEye => Action.TrainedEye,
            ActionType.HeartAndSoul => Action.HeartAndSoul,
            ActionType.PrudentSynthesis => Action.PrudentSynthesis,
            ActionType.TrainedFinesse => Action.TrainedFinesse,
            ActionType.RefinedTouch => Action.RefinedTouch,
            ActionType.QuickInnovation => Action.QuickInnovation,
            ActionType.ImmaculateMend => Action.ImmaculateMend,
            ActionType.TrainedPerfection => Action.TrainedPerfection,
            _ => null,
        };
    }
}
