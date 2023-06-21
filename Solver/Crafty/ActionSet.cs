using Craftimizer.Simulator.Actions;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics.X86;

namespace Craftimizer.Solver.Crafty;

public sealed class ActionSet
{
    private uint Bits { get; set; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int NthBitSet(uint value, int n)
    {
        if (Bmi2.IsSupported)
            return BitOperations.TrailingZeroCount(Bmi2.ParallelBitDeposit(1u << n, value));

        var mask = 0x0000FFFFu;
        var size = 16;
        var _base = 0;

        if (n++ >= BitOperations.PopCount(value))
            return 32;

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
    private static int FromAction(ActionType action) => Array.IndexOf(Simulator.AcceptedActions, action);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ActionType ToAction(int index) => Simulator.AcceptedActions[index];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool HasAction(ActionType action) => (Bits & (1u << (FromAction(action) + 1))) != 0;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddAction(ActionType action) => Bits |= 1u << (FromAction(action) + 1);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RemoveAction(ActionType action) => Bits &= ~(1u << (FromAction(action) + 1));
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ActionType ElementAt(int index) => ToAction(NthBitSet(Bits, index) - 1);

    public int Count => BitOperations.PopCount(Bits);
}