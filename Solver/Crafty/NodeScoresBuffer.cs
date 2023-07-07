using System;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Craftimizer.Solver.Crafty;

// Adapted from https://github.com/dtao/ConcurrentList/blob/4fcf1c76e93021a41af5abb2d61a63caeba2adad/ConcurrentList/ConcurrentList.cs
public struct NodeScoresBuffer
{
    public sealed class ScoresBatch
    {
        public Memory<float> ScoreSum;
        public Memory<float> MaxScore;
        public Memory<int> Visits;

        public ScoresBatch()
        {
            ScoreSum = new float[BatchSize];
            MaxScore = new float[BatchSize];
            Visits = new int[BatchSize];
        }
    }

    // Technically 25, but it's very unlikely to actually get to there.
    // The benchmark reaches 20 at most, but here we have a little leeway just in case.
    private const int MaxSize = 24;

    private static int BatchSize = Vector<float>.Count;
    private static int BatchSizeBits = int.Log2(BatchSize);
    private static int BatchSizeMask = BatchSize - 1;

    private static int BatchCount = MaxSize / BatchSize;

    public ScoresBatch[] Data;
    private int index;
    private int count;

    public readonly int Count => count;

    public void AddConcurrent()
    {
        if (Data == null)
            Interlocked.CompareExchange(ref Data, new ScoresBatch[BatchCount], null);

        var idx = Interlocked.Increment(ref index) - 1;

        var (arrayIdx, _) = GetArrayIndex(idx);

        if (Data[arrayIdx] == null)
            Interlocked.CompareExchange(ref Data[arrayIdx], new ScoresBatch(), null);

        Interlocked.Increment(ref count);
    }

    public void Add()
    {
        Data ??= new ScoresBatch[BatchCount];

        var idx = count++;

        var (arrayIdx, _) = GetArrayIndex(idx);

        Data[arrayIdx] ??= new();
    }

    public readonly void VisitConcurrent((int arrayIdx, int subIdx) at, float score)
    {
        Intrinsics.CASAdd(ref Data[at.arrayIdx].ScoreSum.Span[at.subIdx], score);
        Intrinsics.CASMax(ref Data[at.arrayIdx].MaxScore.Span[at.subIdx], score);
        Interlocked.Increment(ref Data[at.arrayIdx].Visits.Span[at.subIdx]);
    }

    public readonly void Visit((int arrayIdx, int subIdx) at, float score)
    {
        Data[at.arrayIdx].ScoreSum.Span[at.subIdx] += score;
        Data[at.arrayIdx].MaxScore.Span[at.subIdx] = Math.Max(Data[at.arrayIdx].MaxScore.Span[at.subIdx], score);
        Data[at.arrayIdx].Visits.Span[at.subIdx]++;
    }

    public readonly int GetVisits((int arrayIdx, int subIdx) at) =>
        Data[at.arrayIdx].Visits.Span[at.subIdx];

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static (int arrayIdx, int subIdx) GetArrayIndex(int idx) =>
        (idx >> BatchSizeBits, idx & BatchSizeMask);
}
