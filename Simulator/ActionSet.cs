using Craftimizer.Simulator.Actions;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics.X86;

namespace Craftimizer.Simulator;

public record ActionSet
{
    public ulong Bits { get; set; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool HasFlag(ActionType action) => (Bits & (1ul << ((int)action + 1))) != 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetFlag(ActionType action) => Bits |= 1ul << ((int)action + 1);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ClearFlag(ActionType action) => Bits &= ~(1ul << ((int)action + 1));

    public IEnumerable<ActionType> Actions => GetActions();

    private IEnumerable<ActionType> GetActions()
    {
        foreach (var action in Enum.GetValues<ActionType>())
            if (HasFlag(action))
                yield return action;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int NthBitSet(ulong value, int n)
    {
        if (Bmi2.X64.IsSupported)
            return BitOperations.TrailingZeroCount(Bmi2.X64.ParallelBitDeposit(1ul << n, value));

        ulong mask = 0x0000FFFFFFFFu;
        var size = 32;
        var _base = 0;

        if (n++ >= BitOperations.PopCount(value))
            return 64;

        while (size > 0)
        {
            var count = BitOperations.PopCount(value & mask);
            if (n > count)
            {
                _base += size;
                size >>= 1;
                mask |= mask << size;
            }
            else
            {
                size >>= 1;
                mask >>= size;
            }
        }

        return _base;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ActionType ActionAt(int index) => (ActionType)(NthBitSet(Bits, index) - 1);

    public int ActionCount => BitOperations.PopCount(Bits);

    public bool IsEmpty => Bits == 0;
}
