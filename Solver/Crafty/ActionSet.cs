using Craftimizer.Simulator.Actions;
using System.Diagnostics.Contracts;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Craftimizer.Solver.Crafty;

public struct ActionSet
{
    private uint bits;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int FromAction(ActionType action) => Simulator.AcceptedActionsLUT[(byte)action];
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ActionType ToAction(int index) => Simulator.AcceptedActions[index];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddAction(ActionType action) => bits |= 1u << (FromAction(action) + 1);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RemoveAction(ActionType action) => bits &= ~(1u << (FromAction(action) + 1));

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool HasAction(ActionType action) => (bits & (1u << (FromAction(action) + 1))) != 0;
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly ActionType ElementAt(int index) => ToAction(Intrinsics.NthBitSet(bits, index) - 1);

    [Pure]
    public readonly int Count => BitOperations.PopCount(bits);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    //public readonly ActionType SelectRandom(Random random) => ElementAt(random.Next(Count));
    public readonly ActionType SelectRandom(Random random) => First();

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly ActionType First() => ElementAt(0);
}
