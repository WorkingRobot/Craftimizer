using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Craftimizer.Solver;

// Adapted from https://github.com/dtao/ConcurrentList/blob/4fcf1c76e93021a41af5abb2d61a63caeba2adad/ConcurrentList/ConcurrentList.cs
public struct NodeScoresBuffer
{
    [StructLayout(LayoutKind.Auto)]
    public readonly struct ScoresBatch
    {
        public readonly Memory<float> ScoreSum;
        public readonly Memory<float> MaxScore;
        public readonly Memory<int> Visits;

        public ScoresBatch()
        {
            ScoreSum = new float[ArenaBuffer.BatchSize];
            MaxScore = new float[ArenaBuffer.BatchSize];
            Visits = new int[ArenaBuffer.BatchSize];
        }
    }

    public ScoresBatch[] Data;
    public int Count { get; private set; }

    public void Add()
    {
        Data ??= new ScoresBatch[ArenaBuffer.BatchCount];

        var idx = Count++;

        var (arrayIdx, subIdx) = GetArrayIndex(idx);

        if (subIdx == 0)
            Data[arrayIdx] = new();
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
        (idx >> ArenaBuffer.BatchSizeBits, idx & ArenaBuffer.BatchSizeMask);
}
