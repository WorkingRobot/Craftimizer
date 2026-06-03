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

        QualityTarget = config.ResolveQualityTarget(in recipe);

        ActionPool = config.ActionPool;
    }
}
