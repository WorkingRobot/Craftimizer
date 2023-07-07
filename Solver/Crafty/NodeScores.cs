using System.Runtime.InteropServices;

namespace Craftimizer.Solver.Crafty;

[StructLayout(LayoutKind.Auto)]
public struct NodeScores
{
    public float ScoreSum;
    public float MaxScore;
    public int Visits;

    public void VisitConcurrent(float score)
    {
        Intrinsics.CASAdd(ref ScoreSum, score);
        Intrinsics.CASMax(ref MaxScore, score);
        Interlocked.Increment(ref Visits);
    }

    public void Visit(float score)
    {
        ScoreSum += score;
        MaxScore = Math.Max(MaxScore, score);
        Visits++;
    }
}
