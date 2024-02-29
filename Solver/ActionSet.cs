using Craftimizer.Simulator.Actions;
using System.Diagnostics.Contracts;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Craftimizer.Solver;

public struct ActionSet
{
    internal ulong bits;

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int FromAction(ActionType action) => (byte)action;

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ActionType ToAction(int index) => (ActionType)index;

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ulong ToMask(ActionType action) => 1ul << FromAction(action);

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
    public readonly ActionType ElementAt(int index) => ToAction(Intrinsics.NthBitSet(bits, index));

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
