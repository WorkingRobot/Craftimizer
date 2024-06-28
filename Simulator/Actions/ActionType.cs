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
    DaringTouch,
    DelicateSynthesis,
    FinalAppraisal,
    GreatStrides,
    Groundwork,
    HastyTouch,
    HeartAndSoul,
    ImmaculateMend,
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
    QuickInnovation,
    RapidSynthesis,
    RefinedTouch,
    Reflect,
    StandardTouch,
    TrainedEye,
    TrainedFinesse,
    TrainedPerfection,
    TricksOfTheTrade,
    Veneration,
    WasteNot,
    WasteNot2,

    StandardTouchCombo,
    AdvancedTouchCombo,
    ObservedAdvancedTouchCombo,
    RefinedTouchCombo,
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

    public static int Level(this ActionType me) =>
        me.Base().Level;

    public static ActionCategory Category(this ActionType me) =>
        me.Base().Category;
}
