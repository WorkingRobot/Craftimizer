using Craftimizer.Simulator.Actions;
using System.Diagnostics.Contracts;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Craftimizer.Solver;

public struct ActionSet
{
    private uint bits;

    internal static ReadOnlySpan<ActionType> AcceptedActions => new[]
    {
        ActionType.StandardTouchCombo,
        ActionType.AdvancedTouchCombo,
        ActionType.FocusedTouchCombo,
        ActionType.FocusedSynthesisCombo,
        ActionType.TrainedFinesse,
        ActionType.PrudentSynthesis,
        ActionType.Groundwork,
        ActionType.AdvancedTouch,
        ActionType.CarefulSynthesis,
        ActionType.TrainedEye,
        ActionType.DelicateSynthesis,
        ActionType.PreparatoryTouch,
        ActionType.Reflect,
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
        ActionType.MastersMend,
        ActionType.BasicTouch,
    };

    public static readonly int[] AcceptedActionsLUT;

    static ActionSet()
    {
        AcceptedActionsLUT = new int[Enum.GetValues<ActionType>().Length];
        for (var i = 0; i < AcceptedActionsLUT.Length; i++)
            AcceptedActionsLUT[i] = -1;
        for (var i = 0; i < AcceptedActions.Length; i++)
            AcceptedActionsLUT[(byte)AcceptedActions[i]] = i;
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int FromAction(ActionType action)
    {
        var ret = AcceptedActionsLUT[(byte)action];
        if (ret == -1)
            throw new ArgumentOutOfRangeException(nameof(action), action, $"Action {action} is unsupported in {nameof(ActionSet)}.");
        return ret;
    }
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ActionType ToAction(int index)
    {
        if (index < 0 || index >= AcceptedActions.Length)
            throw new ArgumentOutOfRangeException(nameof(index), index, $"Index {index} is out of range for {nameof(ActionSet)}.");
        return AcceptedActions[index];
    }
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint ToMask(ActionType action) => 1u << (FromAction(action) + 1);

    // Return true if action was newly added and not there before.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool AddAction(ActionType action)
    {
        var mask = ToMask(action);
        var old = bits;
        bits |= mask;
        return (old & mask) == 0;
    }

    // Return true if action was newly removed and not already gone.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool RemoveAction(ActionType action)
    {
        var mask = ToMask(action);
        var old = bits;
        bits &= ~mask;
        return (old & mask) != 0;
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool HasAction(ActionType action) => (bits & ToMask(action)) != 0;
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly ActionType ElementAt(int index) => ToAction(Intrinsics.NthBitSet(bits, index) - 1);

    [Pure]
    public readonly int Count => BitOperations.PopCount(bits);

    [Pure]
    public readonly bool IsEmpty => bits == 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly ActionType SelectRandom(Random random)
    {
#if IS_DETERMINISTIC
        return First();
#else
        return ElementAt(random.Next(Count));
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ActionType PopRandom(Random random)
    {
#if IS_DETERMINISTIC
        return PopFirst();
#else
        var action = ElementAt(random.Next(Count));
        RemoveAction(action);
        return action;
#endif
    }

#if IS_DETERMINISTIC
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ActionType PopFirst()
    {
        var action = First();
        RemoveAction(action);
        return action;
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private readonly ActionType First() => ElementAt(0);
#endif
}
