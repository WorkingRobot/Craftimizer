using System.Runtime.InteropServices;

namespace Craftimizer.Solver.Crafty;

[StructLayout(LayoutKind.Auto)]
public readonly struct SolverConfig
{
    public readonly int Iterations;
    public readonly float ScoreStorageThreshold;
    public readonly float MaxScoreWeightingConstant;
    public readonly float ExplorationConstant;
    public readonly int MaxStepCount;

    public SolverConfig() : this(30000, 1f, 0.1f, 4, 25) { }

    public SolverConfig(
        int iterations = 30000,
        float scoreStorageThreshold = 1f,
        float maxScoreWeightingConstant = 0.1f,
        float explorationConstant = 4f,
        int maxStepCount = 25)
    {
        Iterations = iterations;
        ScoreStorageThreshold = scoreStorageThreshold;
        MaxScoreWeightingConstant = maxScoreWeightingConstant;
        ExplorationConstant = explorationConstant;
        MaxStepCount = maxStepCount;
    }
}
