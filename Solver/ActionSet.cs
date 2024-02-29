using Craftimizer.Simulator.Actions;
using System.Diagnostics.Contracts;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Craftimizer.Solver;

public struct ActionSet
{
    internal uint bits;

    // Return true if action was newly added and not there before.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool AddAction(in ActionPool pool, ActionType action)
    {
        var mask = pool.ToMask(action);
        var old = bits;
        bits |= mask;
        return (old & mask) == 0;
    }

    // Return true if action was newly removed and not already gone.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool RemoveAction(in ActionPool pool, ActionType action)
    {
        var mask = pool.ToMask(action);
        var old = bits;
        bits &= ~mask;
        return (old & mask) != 0;
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool HasAction(in ActionPool pool, ActionType action) => (bits & pool.ToMask(action)) != 0;
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly ActionType ElementAt(in ActionPool pool, int index) => pool.ToAction(Intrinsics.NthBitSet(bits, index) - 1);

    [Pure]
    public readonly int Count => BitOperations.PopCount(bits);

    [Pure]
    public readonly bool IsEmpty => bits == 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly ActionType SelectRandom(in ActionPool pool, Random random)
    {
#if IS_DETERMINISTIC
        return First(in pool);
#else
        return ElementAt(in pool, random.Next(Count));
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ActionType PopRandom(in ActionPool pool, Random random)
    {
#if IS_DETERMINISTIC
        return PopFirst(in pool);
#else
        var action = ElementAt(in pool, random.Next(Count));
        RemoveAction(in pool, action);
        return action;
#endif
    }

#if IS_DETERMINISTIC
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ActionType PopFirst(in pool)
    {
        var action = First(in pool);
        RemoveAction(in pool, action);
        return action;
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private readonly ActionType First(in pool) => ElementAt(in pool, 0);
#endif
}
