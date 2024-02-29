using Craftimizer.Simulator.Actions;
using System.Diagnostics.Contracts;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Craftimizer.Solver;

[StructLayout(LayoutKind.Auto)]
public readonly struct ActionPool
{
    public static ActionPool Default { get; } = new(new[]
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
    });

    public const int MaskSize = 32;

    // Bitmask of accepted actions
    private readonly ulong acceptedActions;

    internal ActionType[] AcceptedActions => GetActions();
    internal int Count => BitOperations.PopCount(acceptedActions);

    public ActionPool(ReadOnlySpan<ActionType> actions)
    {
        acceptedActions = 0;
        foreach (var action in actions)
            acceptedActions |= 1ul << ((byte)action);

        if (Count > MaskSize)
            throw new ArgumentOutOfRangeException(nameof(actions), actions.Length, $"ActionPool only supports up to {MaskSize} actions");
    }

    private ActionType[] GetActions()
    {
        var ret = new ActionType[Count];
        var i = 0;
        foreach (var v in (byte[])Enum.GetValuesAsUnderlyingType<ActionType>())
        {
            if ((acceptedActions & (1ul << v)) != 0)
                ret[i++] = (ActionType)v;
        }
        return ret;
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int FromAction(ActionType action)
    {
        if ((acceptedActions & (1ul << (byte)action)) == 0)
            throw new ArgumentOutOfRangeException(nameof(action), action, "Action is not accepted by this pool.");

        // Get number of 1s before action
        return BitOperations.PopCount(acceptedActions & ((1ul << (byte)action) - 1));
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ActionType ToAction(int index)
    {
        if (index < 0 || index >= Count)
            throw new ArgumentOutOfRangeException(nameof(index), index, $"Index {index} is out of range for this pool.");

        // Get index of (index+1)th 1 in set
        return (ActionType)Intrinsics.NthBitSet(acceptedActions, index);
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal uint ToMask(ActionType action) => 1u << (FromAction(action) + 1);
}
