using System.Runtime.InteropServices;

namespace Craftimizer.Solver.Crafty;

[StructLayout(LayoutKind.Auto)]
public readonly record struct SolverConfig
{
    public int Iterations { get; init; }
    public float ScoreStorageThreshold { get; init; }
    public float MaxScoreWeightingConstant { get; init; }
    public float ExplorationConstant { get; init; }
    public int MaxStepCount { get; init; }
    public int MaxRolloutStepCount { get; init; }
    public int ThreadCount { get; init; }

    public SolverConfig()
    {
        Iterations = 300000;
        ScoreStorageThreshold = 1f;
        MaxScoreWeightingConstant = 0.1f;
        ExplorationConstant = 2;
        MaxStepCount = 25;
        MaxRolloutStepCount = 99;
        ThreadCount = Environment.ProcessorCount;
    }
}
