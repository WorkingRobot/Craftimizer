using System.Diagnostics.Contracts;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Craftimizer.Solver.Crafty;

public sealed class ArenaNode<T> where T : struct
{
    // Adapted from https://github.com/dtao/ConcurrentList/blob/4fcf1c76e93021a41af5abb2d61a63caeba2adad/ConcurrentList/ConcurrentList.cs
    public struct ChildBuffer
    {
        // Technically 25, but it's very unlikely to actually get to there.
        // The benchmark reaches 20 at most, but here we have a little leeway just in case.
        private const int MaxSize = 24;

        private static int BatchSize = Vector<float>.Count;
        private static int BatchSizeBits = int.Log2(BatchSize);
        private static int BatchSizeMask = BatchSize - 1;

        private static int BatchCount = MaxSize / BatchSize;

        public ArenaNode<T>[][] Data;
        private int index;
        private int count;

        public readonly int Count => count;

        public void Add(ArenaNode<T> node)
        {
            if (Data == null)
                Interlocked.CompareExchange(ref Data, new ArenaNode<T>[BatchCount][], null);

            var index = Interlocked.Increment(ref this.index) - 1;

            var (arrayIdx, subIdx) = GetArrayIndex(index);

            if (Data[arrayIdx] == null)
                Interlocked.CompareExchange(ref Data[arrayIdx], new ArenaNode<T>[BatchSize], null);

            Data[arrayIdx][subIdx] = node;

            Interlocked.Increment(ref count);
        }

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static (int arrayIdx, int subIdx) GetArrayIndex(int idx) =>
            (idx >> BatchSizeBits, idx & BatchSizeMask);
    }

    public T State;
    public ChildBuffer Children;
    public readonly ArenaNode<T>? Parent;

    public ArenaNode(T state, ArenaNode<T>? parent = null)
    {
        State = state;
        Children = new();
        Parent = parent;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ArenaNode<T> Add(T state)
    {
        var node = new ArenaNode<T>(state, this);
        Children.Add(node);
        return node;
    }
}
