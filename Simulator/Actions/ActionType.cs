using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;

namespace Craftimizer.Simulator.Actions;

public enum ActionType : byte
{
    AdvancedTouch,
    BasicSynthesis,
    BasicTouch,
    ByregotsBlessing,
    CarefulObservation,
    CarefulSynthesis,
    DelicateSynthesis,
    FinalAppraisal,
    FocusedSynthesis,
    FocusedTouch,
    GreatStrides,
    Groundwork,
    HastyTouch,
    HeartAndSoul,
    Innovation,
    IntensiveSynthesis,
    Manipulation,
    MastersMend,
    MuscleMemory,
    Observe,
    PreciseTouch,
    PreparatoryTouch,
    PrudentSynthesis,
    PrudentTouch,
    RapidSynthesis,
    Reflect,
    StandardTouch,
    TrainedEye,
    TrainedFinesse,
    TricksOfTheTrade,
    Veneration,
    WasteNot,
    WasteNot2,
}

public static class ActionUtils
{
    private static readonly BaseAction[] Actions;
    
    static ActionUtils()
    {
        var types = typeof(BaseAction).Assembly.GetTypes()
            .Where(t => t.IsAssignableTo(typeof(BaseAction)) && !t.IsAbstract);
        Actions = Enum.GetNames<ActionType>()
            .Select(a => types.First(t => t.Name.Equals(a, StringComparison.Ordinal)))
            .Select(t => (Activator.CreateInstance(t) as BaseAction)!)
            .ToArray();
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BaseAction Base(this ActionType me) => Actions[(int)me];

    public static IEnumerable<ActionType> AvailableActions(Simulator simulation) =>
        simulation.IsComplete
            ? Enumerable.Empty<ActionType>()
            : Enum.GetValues<ActionType>()
                .Where(a => a.Base().CanUse(simulation));

    public static int Level(this ActionType me) =>
        me.Base().Level;

    public static ActionCategory Category(this ActionType me) =>
        me.Base().Category;

    public static string IntName(this ActionType me) =>
        me switch
        {
            ActionType.AdvancedTouch => "Advanced Touch",
            ActionType.BasicSynthesis => "Basic Synthesis",
            ActionType.BasicTouch => "Basic Touch",
            ActionType.ByregotsBlessing => "Byregot's Blessing",
            ActionType.CarefulObservation => "Careful Observation",
            ActionType.CarefulSynthesis => "Careful Synthesis",
            ActionType.DelicateSynthesis => "Delicate Synthesis",
            ActionType.FinalAppraisal => "Final Appraisal",
            ActionType.FocusedSynthesis => "Focused Synthesis",
            ActionType.FocusedTouch => "Focused Touch",
            ActionType.GreatStrides => "Great Strides",
            ActionType.Groundwork => "Groundwork",
            ActionType.HastyTouch => "Hasty Touch",
            ActionType.HeartAndSoul => "Heart And Soul",
            ActionType.Innovation => "Innovation",
            ActionType.IntensiveSynthesis => "Intensive Synthesis",
            ActionType.Manipulation => "Manipulation",
            ActionType.MastersMend => "Master's Mend",
            ActionType.MuscleMemory => "Muscle Memory",
            ActionType.Observe => "Observe",
            ActionType.PreciseTouch => "Precise Touch",
            ActionType.PreparatoryTouch => "Preparatory Touch",
            ActionType.PrudentSynthesis => "Prudent Synthesis",
            ActionType.PrudentTouch => "Prudent Touch",
            ActionType.RapidSynthesis => "Rapid Synthesis",
            ActionType.Reflect => "Reflect",
            ActionType.StandardTouch => "Standard Touch",
            ActionType.TrainedEye => "Trained Eye",
            ActionType.TrainedFinesse => "Trained Finesse",
            ActionType.TricksOfTheTrade => "Tricks Of The Trade",
            ActionType.Veneration => "Veneration",
            ActionType.WasteNot => "Waste Not",
            ActionType.WasteNot2 => "Waste Not II",
            _ => me.ToString(),
        };
}
