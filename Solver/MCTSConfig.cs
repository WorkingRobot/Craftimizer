using Craftimizer.Simulator;
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

    // Absolute quality value the score rewards up to (resolved once from the config + recipe).
    public int QualityTarget { get; init; }

    public ActionType[] ActionPool { get; init; }

    public MCTSConfig(in SolverConfig config, in RecipeInfo recipe)
    {
        MaxStepCount = config.MaxStepCount;
        MaxRolloutStepCount = config.MaxRolloutStepCount;
        StrictActions = config.StrictActions;

        MaxScoreWeightingConstant = config.MaxScoreWeightingConstant;
        ExplorationConstant = config.ExplorationConstant;

        var total = config.ScoreProgress +
                    config.ScoreQuality +
                    config.ScoreDurability +
                    config.ScoreCP +
                    config.ScoreSteps;

        ScoreProgress = config.ScoreProgress / total;
        ScoreQuality = config.ScoreQuality / total;
        ScoreDurability = config.ScoreDurability / total;
        ScoreCP = config.ScoreCP / total;
        ScoreSteps = config.ScoreSteps / total;

        QualityTarget = config.ResolveQualityTarget(in recipe);

        ActionPool = config.ActionPool;
    }
}
