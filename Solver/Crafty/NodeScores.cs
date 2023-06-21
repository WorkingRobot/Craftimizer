namespace Craftimizer.Solver.Crafty;

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
