using Craftimizer.Simulator.Actions;
using System;
using System.Diagnostics.Contracts;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Craftimizer.Solver.Crafty;

public struct ActionSet
{
    private uint bits;

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int FromAction(ActionType action) => Simulator.AcceptedActionsLUT[(byte)action];
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ActionType ToAction(int index) => Simulator.AcceptedActions[index];
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint ToMask(ActionType action) => 1u << (FromAction(action) + 1);

    // Return true if action was newly added and not there before.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool AddAction(ActionType action)
    {
        var mask = ToMask(action);
        var old = Interlocked.Or(ref bits, mask);
        return (old & mask) == 0;
    }

    // Return true if action was newly removed and not already gone.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool RemoveAction(ActionType action)
    {
        var mask = ToMask(action);
        var old = Interlocked.And(ref bits, ~mask);
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
    //public readonly ActionType SelectRandom(Random random) => ElementAt(random.Next(Count));
    public readonly ActionType SelectRandom(Random random) => First();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    //public ActionType? PopRandom(Random random) => PopFirst();
    public ActionType? PopRandom(Random random)
    {
        uint snapshot;
        uint newValue;
        ActionType action;
        do
        {
            snapshot = bits;
            if (snapshot == 0)
                return null;

            var count = BitOperations.PopCount(snapshot);
            var index = random.Next(count);

            action = ToAction(Intrinsics.NthBitSet(snapshot, index) - 1);
            newValue = snapshot & ~ToMask(action);
        }
        while (Interlocked.CompareExchange(ref bits, newValue, snapshot) != snapshot);
        return action;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ActionType? PopFirst()
    {
        uint snapshot;
        uint newValue;
        ActionType action;
        int i = 0;
        do
        {
            ++i;
            snapshot = bits;
            if (snapshot == 0)
                return null;

            var index = 0;

            action = ToAction(Intrinsics.NthBitSet(snapshot, index) - 1);
            newValue = snapshot & ~ToMask(action);
        }
        while (Interlocked.CompareExchange(ref bits, newValue, snapshot) != snapshot);
        //if (i != 1)
            //Console.WriteLine($"Retried {i-1} times");
        return action;
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly ActionType First() => ElementAt(0);
}
