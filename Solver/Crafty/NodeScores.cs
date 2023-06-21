using System.Runtime.InteropServices;

namespace Craftimizer.Solver.Crafty;

[StructLayout(LayoutKind.Auto)]
public struct NodeScores
{
    public float ScoreSum;
    public float MaxScore;
    public float Visits;

    public void Visit(float score)
    {
        ScoreSum += score;
        MaxScore = Math.Max(MaxScore, score);
        Visits++;
    }
}
