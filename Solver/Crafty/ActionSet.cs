using Craftimizer.Simulator.Actions;
using System.Diagnostics.Contracts;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Craftimizer.Solver.Crafty;

public struct ActionSet
{
    private const bool IsDeterministic = false;

    private uint bits;

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int FromAction(ActionType action) => Simulator.AcceptedActionsLUT[(byte)action];
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ActionType ToAction(int index) => Simulator.AcceptedActions[index];
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint ToMask(ActionType action) => 1u << FromAction(action) + 1;

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
        if (IsDeterministic)
            return First();

        return ElementAt(random.Next(Count));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ActionType PopRandom(Random random)
    {
        if (IsDeterministic)
            return PopFirst();

        var action = ElementAt(random.Next(Count));
        RemoveAction(action);
        return action;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ActionType PopFirst()
    {
        var action = First();
        RemoveAction(action);
        return action;
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly ActionType First() => ElementAt(0);
}
