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
    public int ForkCount { get; init; }
    public int FurcatedActionCount { get; init; }
    public bool StrictActions { get; init; }

    public float ScoreProgressBonus { get; init; }
    public float ScoreQualityBonus { get; init; }
    public float ScoreDurabilityBonus { get; init; }
    public float ScoreCPBonus { get; init; }
    public float ScoreFewerStepsBonus { get; init; }

    public SolverConfig()
    {
        Iterations = 300000;
        ScoreStorageThreshold = 1f;
        MaxScoreWeightingConstant = 0.1f;
        ExplorationConstant = 4;
        MaxStepCount = 25;
        MaxRolloutStepCount = MaxStepCount;
        ForkCount = Environment.ProcessorCount;
        FurcatedActionCount = ForkCount / 2;
        StrictActions = true;

        ScoreProgressBonus = .20f;
        ScoreQualityBonus = .65f;
        ScoreDurabilityBonus = .05f;
        ScoreCPBonus = .05f;
        ScoreFewerStepsBonus = .05f;
    }
}
