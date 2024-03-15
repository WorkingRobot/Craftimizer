using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace Craftimizer.Solver;

public struct NodeScoresBuffer
{
    [StructLayout(LayoutKind.Auto)]
    public struct ScoresBatch
    {
        public Vector256<float> ScoreSum;
        public Vector256<float> MaxScore;
        public Vector256<int> Visits;
    }

    public ScoresBatch[]? Data;
    public int Count { get; private set; }

    public void Add()
    {
        Data ??= GC.AllocateUninitializedArray<ScoresBatch>(ArenaBuffer.BatchCount);
        var count = Count++;
        if ((count & ArenaBuffer.BatchSizeMask) == 0)
            Data[count >> ArenaBuffer.BatchSizeBits] = new();
    }

    public readonly void Visit((int arrayIdx, int subIdx) at, float score)
    {
        ref var batch = ref Data![at.arrayIdx];
        batch.ScoreSum.At(at.subIdx) += score;
        ref var maxScore = ref batch.MaxScore.At(at.subIdx);
        maxScore = Math.Max(maxScore, score);
        batch.Visits.At(at.subIdx)++;
    }

    public readonly int GetVisits((int arrayIdx, int subIdx) at) =>
        Data![at.arrayIdx].Visits[at.subIdx];
}

internal static class VectorUtils
{
    public static ref T At<T>(this ref Vector256<T> me, int idx) =>
        ref Unsafe.Add(ref Unsafe.As<Vector256<T>, T>(ref me), idx);
}
