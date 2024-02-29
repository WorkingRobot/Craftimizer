using Craftimizer.Simulator.Actions;
using System.Diagnostics.Contracts;
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
    public const int EnumSize = 37;

    private unsafe struct EnumBuffer
    {
        private fixed byte data[MaskSize];

        public ActionType this[int index] => (ActionType)data[index];

        public EnumBuffer(ReadOnlySpan<ActionType> actions)
        {
            fixed (byte* dataPtr = data)
                actions.CopyTo(new Span<ActionType>(dataPtr, MaskSize));
        }

        public readonly ActionType[] ToArray(int size)
        {
            fixed (byte* dataPtr = data)
                return new Span<ActionType>(dataPtr, size).ToArray();
        }
    }

    private unsafe struct LUTBuffer
    {
        private fixed byte data[EnumSize];

        public byte this[ActionType index] => data[(byte)index];

        public LUTBuffer(ReadOnlySpan<ActionType> actions)
        {
            for (var i = 0; i < EnumSize; i++)
                data[i] = 0xFF;
            for (var i = 0; i < actions.Length; i++)
                data[(byte)actions[i]] = (byte)i;
        }
    }

    // List of accepted actions (max 32)
    private readonly EnumBuffer acceptedActions;
    // Lookup table for accepted actions (ActionType as idx -> idx in acceptedActions)
    private readonly LUTBuffer acceptedActionsLUT;
    private readonly byte size;

    internal ActionType[] AcceptedActions => acceptedActions.ToArray(size);

    public ActionPool(ReadOnlySpan<ActionType> actions)
    {
        if (actions.Length > MaskSize)
            throw new ArgumentOutOfRangeException(nameof(actions), actions.Length, $"ActionPool only supports up to {MaskSize} actions");

        acceptedActions = new(actions);
        acceptedActionsLUT = new(actions);
        size = (byte)actions.Length;
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal byte FromAction(ActionType action)
    {
        var ret = acceptedActionsLUT[action];
        if (ret == 0xFF)
            throw new ArgumentOutOfRangeException(nameof(action), action, $"Action {action} is unsupported in this pool.");
        return ret;
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ActionType ToAction(byte index)
    {
        if (index < 0 || index >= size)
            throw new ArgumentOutOfRangeException(nameof(index), index, $"Index {index} is out of range for this pool.");
        return acceptedActions[index];
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal uint ToMask(ActionType action) => 1u << (FromAction(action) + 1);
}
