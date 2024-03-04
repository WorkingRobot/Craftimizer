using System.Diagnostics.Contracts;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Craftimizer.Solver;

public struct ArenaBuffer
{
    // Technically 25, but it's very unlikely to actually get to there.
    // The benchmark reaches 20 at most, but here we have a little leeway just in case.
    internal const int MaxSize = 32;

    internal static readonly int BatchSize = Vector<float>.Count;
    internal static readonly int BatchSizeBits = int.Log2(BatchSize);
    internal static readonly int BatchSizeMask = BatchSize - 1;

    internal static readonly int BatchCount = MaxSize / BatchSize;
}

// Adapted from https://github.com/dtao/ConcurrentList/blob/4fcf1c76e93021a41af5abb2d61a63caeba2adad/ConcurrentList/ConcurrentList.cs
public struct ArenaBuffer<T> where T : struct
{
    public ArenaNode<T>[][] Data;
    public int Count { get; private set; }

    public void Add(ArenaNode<T> node)
    {
        Data ??= new ArenaNode<T>[ArenaBuffer.BatchCount][];

        var idx = Count++;

        var (arrayIdx, subIdx) = GetArrayIndex(idx);

        Data[arrayIdx] ??= new ArenaNode<T>[ArenaBuffer.BatchSize];

        node.ChildIdx = (arrayIdx, subIdx);
        Data[arrayIdx][subIdx] = node;
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static (int arrayIdx, int subIdx) GetArrayIndex(int idx) =>
        (idx >> ArenaBuffer.BatchSizeBits, idx & ArenaBuffer.BatchSizeMask);
}
