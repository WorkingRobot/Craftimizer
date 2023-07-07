using System.Diagnostics.Contracts;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Craftimizer.Solver.Crafty;

// Adapted from https://github.com/dtao/ConcurrentList/blob/4fcf1c76e93021a41af5abb2d61a63caeba2adad/ConcurrentList/ConcurrentList.cs
public struct ArenaBuffer<T>
{
    // Technically 25, but it's very unlikely to actually get to there.
    // The benchmark reaches 20 at most, but here we have a little leeway just in case.
    private const int MaxSize = 24;

    private static int BatchSize = Vector<float>.Count;
    private static int BatchSizeBits = int.Log2(BatchSize);
    private static int BatchSizeMask = BatchSize - 1;

    private static int BatchCount = MaxSize / BatchSize;

    public T[][] Data;
    private int index; // Unused in single threaded workload
    private int count;

    public readonly int Count => count;

    public void AddConcurrent(T node)
    {
        if (Data == null)
            Interlocked.CompareExchange(ref Data, new T[BatchCount][], null);

        var idx = Interlocked.Increment(ref index) - 1;

        var (arrayIdx, subIdx) = GetArrayIndex(idx);

        if (Data[arrayIdx] == null)
            Interlocked.CompareExchange(ref Data[arrayIdx], new T[BatchSize], null);

        Data[arrayIdx][subIdx] = node;

        Interlocked.Increment(ref count);
    }

    public void Add(T node)
    {
        Data ??= new T[BatchCount][];

        var idx = count++;

        var (arrayIdx, subIdx) = GetArrayIndex(idx);

        Data[arrayIdx] ??= new T[BatchSize];

        Data[arrayIdx][subIdx] = node;
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static (int arrayIdx, int subIdx) GetArrayIndex(int idx) =>
        (idx >> BatchSizeBits, idx & BatchSizeMask);
}
