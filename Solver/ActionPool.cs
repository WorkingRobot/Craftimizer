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
        public fixed byte Data[MaskSize];

        public ref ActionType this[int index] => ref Unsafe.As<byte, ActionType>(ref Data[index]);

        public Span<ActionType> AsSpan() => new(Unsafe.AsPointer(ref this[0]), MaskSize);
    }

    private unsafe struct LUTBuffer
    {
        public fixed byte Data[EnumSize];

        public ref byte this[ActionType index] => ref Data[(byte)index];

#pragma warning disable MA0099
        public Span<byte> AsSpan() => new(Unsafe.AsPointer(ref this[0]), EnumSize);
#pragma warning restore MA0099
    }

    // List of accepted actions (max 32)
    private readonly EnumBuffer acceptedActions;
    // Lookup table for accepted actions (ActionType as idx -> idx in acceptedActions)
    private readonly LUTBuffer acceptedActionsLUT;
    private readonly byte size;

    internal ReadOnlySpan<ActionType> AcceptedActions => acceptedActions.AsSpan().Slice(0, size);

    public ActionPool(ReadOnlySpan<ActionType> actions)
    {
        if (actions.Length > MaskSize)
            throw new ArgumentOutOfRangeException(nameof(actions), actions.Length, $"ActionPool only supports up to {MaskSize} actions");

        size = (byte)actions.Length;

        acceptedActions.AsSpan().Fill((ActionType)0xFF);
        acceptedActionsLUT.AsSpan().Fill(0xFF);

        actions.CopyTo(acceptedActions.AsSpan());

        for (var i = 0; i < size; i++)
            acceptedActionsLUT[acceptedActions[i]] = (byte)i;
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
