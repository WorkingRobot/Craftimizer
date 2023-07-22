using System.Runtime.InteropServices;

namespace Craftimizer.Solver.Crafty;

public enum SolverAlgorithm
{
    Oneshot,
    OneshotForked,
    Stepwise,
    StepwiseForked,
    StepwiseFurcated,
}

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

    public SolverAlgorithm Algorithm { get; init; }

    public SolverConfig()
    {
        Iterations = 100000;
        ScoreStorageThreshold = 1f;
        MaxScoreWeightingConstant = 0.1f;
        ExplorationConstant = 4;
        MaxStepCount = 30;
        MaxRolloutStepCount = 99;
        ForkCount = Math.Max(Environment.ProcessorCount, 32);
        FurcatedActionCount = ForkCount / 2;
        StrictActions = true;

        ScoreProgressBonus = .20f;
        ScoreQualityBonus = .65f;
        ScoreDurabilityBonus = .05f;
        ScoreCPBonus = .05f;
        ScoreFewerStepsBonus = .05f;

        Algorithm = SolverAlgorithm.StepwiseFurcated;
    }

    public static readonly SolverConfig SimulatorDefault = new SolverConfig() with
    {

    };

    public static readonly SolverConfig SynthHelperDefault = new SolverConfig() with
    {
        Iterations = 300000,
        ForkCount = Environment.ProcessorCount - 1, // Keep one for the game thread
        FurcatedActionCount = Environment.ProcessorCount / 2,
        Algorithm = SolverAlgorithm.StepwiseForked
    };
}
