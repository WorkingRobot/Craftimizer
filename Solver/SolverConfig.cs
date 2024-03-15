using Craftimizer.Simulator.Actions;
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
        Iterations = 100000;
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

        ActionPool = DefaultActionPool;
        Algorithm = SolverAlgorithm.StepwiseFurcated;
    }

    public static ActionType[] OptimizeActionPool(IEnumerable<ActionType> actions) =>
        actions.Order().ToArray();

    public static readonly ActionType[] DefaultActionPool = OptimizeActionPool(new[]
    {
        ActionType.StandardTouchCombo,
        ActionType.AdvancedTouchCombo,
        ActionType.FocusedTouchCombo,
        ActionType.FocusedSynthesisCombo,
        ActionType.TrainedFinesse,
        ActionType.PrudentSynthesis,
        ActionType.Groundwork,
        ActionType.AdvancedTouch,
        ActionType.CarefulSynthesis,
        ActionType.TrainedEye,
        ActionType.DelicateSynthesis,
        ActionType.PreparatoryTouch,
        ActionType.Reflect,
        ActionType.PrudentTouch,
        ActionType.Manipulation,
        ActionType.MuscleMemory,
        ActionType.ByregotsBlessing,
        ActionType.WasteNot2,
        ActionType.BasicSynthesis,
        ActionType.Innovation,
        ActionType.GreatStrides,
        ActionType.StandardTouch,
        ActionType.Veneration,
        ActionType.WasteNot,
        ActionType.MastersMend,
        ActionType.BasicTouch,
    });

    public static readonly IReadOnlySet<ActionType> InefficientActions = new HashSet<ActionType>(new[]
    {
        ActionType.CarefulObservation,
        ActionType.HeartAndSoul,
        ActionType.FinalAppraisal
    });

    public static readonly IReadOnlySet<ActionType> RiskyActions = new HashSet<ActionType>(new[]
    {
        ActionType.RapidSynthesis,
        ActionType.HastyTouch,
    });

    public static readonly SolverConfig SimulatorDefault = new SolverConfig() with
    {

    };

    public static readonly SolverConfig SynthHelperDefault = new SolverConfig() with
    {
        // Add properties if necessary
    };
}
