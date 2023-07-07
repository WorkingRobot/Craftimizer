using System.Runtime.InteropServices;

namespace Craftimizer.Solver.Crafty;

[StructLayout(LayoutKind.Auto)]
public sealed class RootScores
{
    public float ScoreSum;
    public float MaxScore;
    public int Visits;

    public void Visit(float score)
    {
        ScoreSum += score;
        MaxScore = Math.Max(MaxScore, score);
        Visits++;
    }
}
