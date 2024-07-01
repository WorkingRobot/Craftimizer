using Craftimizer.Simulator.Actions;
using System.Runtime.InteropServices;

namespace Craftimizer.Solver;

[StructLayout(LayoutKind.Auto)]
public readonly record struct MCTSConfig
{
    public int MaxThreadCount { get; init; }

    public int MaxStepCount { get; init; }
    public int MaxRolloutStepCount { get; init; }
    public bool StrictActions { get; init; }

    public float MaxScoreWeightingConstant { get; init; }
    public float ExplorationConstant { get; init; }

    public float ScoreProgress { get; init; }
    public float ScoreQuality { get; init; }
    public float ScoreDurability { get; init; }
    public float ScoreCP { get; init; }
    public float ScoreSteps { get; init; }

    public ActionType[] ActionPool { get; init; }

    public MCTSConfig(in SolverConfig config)
    {
        MaxStepCount = config.MaxStepCount;
        MaxRolloutStepCount = config.MaxRolloutStepCount;
        StrictActions = config.StrictActions;

        MaxScoreWeightingConstant = config.MaxScoreWeightingConstant;
        ExplorationConstant = config.ExplorationConstant;

        ScoreProgress = config.ScoreProgress;
        ScoreQuality = config.ScoreQuality;
        ScoreDurability = config.ScoreDurability;
        ScoreCP = config.ScoreCP;
        ScoreSteps = config.ScoreSteps;

        ActionPool = config.ActionPool;
    }
}
