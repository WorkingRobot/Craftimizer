using Craftimizer.Simulator.Actions;
using System.Collections.Frozen;
using System.Runtime.InteropServices;

namespace Craftimizer.Solver;

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
    public int MaxThreadCount { get; init; }
    public int ForkCount { get; init; }
    public int FurcatedActionCount { get; init; }
    public bool StrictActions { get; init; }

    public float ScoreProgress { get; init; }
    public float ScoreQuality { get; init; }
    public float ScoreDurability { get; init; }
    public float ScoreCP { get; init; }
    public float ScoreSteps { get; init; }

    public ActionType[] ActionPool { get; init; }
    public SolverAlgorithm Algorithm { get; init; }

    public SolverConfig()
    {
        Iterations = 100_000;
        ScoreStorageThreshold = 1f;
        MaxScoreWeightingConstant = 0.1f;
        ExplorationConstant = 4;
        MaxStepCount = 30;
        MaxRolloutStepCount = 99;
        // Use 80% of all cores if less than 20 cores are available, otherwise use all but 4 cores. Keep at least 1 core.
        MaxThreadCount = Math.Max(1, Math.Max(Environment.ProcessorCount - 4, (int)MathF.Floor(Environment.ProcessorCount * 0.8f)));
        // Use 32 forks at minimum, or the number of cores, whichever is higher.
        ForkCount = Math.Max(Environment.ProcessorCount, 32);
        FurcatedActionCount = ForkCount / 2;
        StrictActions = true;

        ScoreProgress = .20f;
        ScoreQuality = .65f;
        ScoreDurability = .05f;
        ScoreCP = .05f;
        ScoreSteps = .05f;

        ActionPool = DeterministicActionPool;
        Algorithm = SolverAlgorithm.StepwiseFurcated;
    }

    public static ActionType[] OptimizeActionPool(IEnumerable<ActionType> actions) =>
        [.. actions.Order()];

    public static readonly ActionType[] DeterministicActionPool = OptimizeActionPool(new[]
    {
        ActionType.MuscleMemory,
        ActionType.Reflect,
        ActionType.TrainedEye,

        ActionType.BasicSynthesis,
        ActionType.CarefulSynthesis,
        ActionType.Groundwork,
        ActionType.DelicateSynthesis,
        ActionType.PrudentSynthesis,

        ActionType.BasicTouch,
        ActionType.StandardTouch,
        ActionType.ByregotsBlessing,
        ActionType.PrudentTouch,
        ActionType.AdvancedTouch,
        ActionType.PreparatoryTouch,
        ActionType.TrainedFinesse,
        ActionType.RefinedTouch,

        ActionType.MastersMend,
        ActionType.WasteNot,
        ActionType.WasteNot2,
        ActionType.Manipulation,
        ActionType.ImmaculateMend,
        ActionType.TrainedPerfection,

        ActionType.Veneration,
        ActionType.GreatStrides,
        ActionType.Innovation,
        ActionType.QuickInnovation,

        ActionType.Observe,
        ActionType.HeartAndSoul,

        ActionType.StandardTouchCombo,
        ActionType.AdvancedTouchCombo,
        ActionType.ObservedAdvancedTouchCombo,
        ActionType.RefinedTouchCombo,
    });

    // Same as deterministic, but with condition-specific actions added
    public static readonly ActionType[] RandomizedActionPool = OptimizeActionPool(new[]
    {
        ActionType.MuscleMemory,
        ActionType.Reflect,
        ActionType.TrainedEye,

        ActionType.BasicSynthesis,
        ActionType.CarefulSynthesis,
        ActionType.Groundwork,
        ActionType.DelicateSynthesis,
        ActionType.IntensiveSynthesis,
        ActionType.PrudentSynthesis,

        ActionType.BasicTouch,
        ActionType.StandardTouch,
        ActionType.ByregotsBlessing,
        ActionType.PreciseTouch,
        ActionType.PrudentTouch,
        ActionType.AdvancedTouch,
        ActionType.PreparatoryTouch,
        ActionType.TrainedFinesse,
        ActionType.RefinedTouch,

        ActionType.MastersMend,
        ActionType.WasteNot,
        ActionType.WasteNot2,
        ActionType.Manipulation,
        ActionType.ImmaculateMend,
        ActionType.TrainedPerfection,

        ActionType.Veneration,
        ActionType.GreatStrides,
        ActionType.Innovation,
        ActionType.QuickInnovation,

        ActionType.Observe,
        ActionType.HeartAndSoul,
        ActionType.TricksOfTheTrade,

        ActionType.StandardTouchCombo,
        ActionType.AdvancedTouchCombo,
        ActionType.ObservedAdvancedTouchCombo,
        ActionType.RefinedTouchCombo,
    });

    public static readonly FrozenSet<ActionType> InefficientActions =
        new[]
        {
            ActionType.CarefulObservation,
            ActionType.FinalAppraisal
        }.ToFrozenSet();

    public static readonly FrozenSet<ActionType> RiskyActions =
        new[]
        {
            ActionType.RapidSynthesis,
            ActionType.HastyTouch,
            ActionType.DaringTouch,
        }.ToFrozenSet();

    public static readonly SolverConfig RecipeNoteDefault = new SolverConfig() with
    {

    };

    public static readonly SolverConfig EditorDefault = new SolverConfig() with
    {
        Iterations = 500000
    };

    public static readonly SolverConfig SynthHelperDefault = new SolverConfig() with
    {
        ActionPool = RandomizedActionPool
    };
}
