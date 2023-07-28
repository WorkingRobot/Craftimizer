using Craftimizer.Simulator.Actions;
using Craftimizer.Simulator;

namespace Craftimizer.Solver.Algorithms;

public enum AlgorithmType
{
    Oneshot,
    OneshotForked,
    Stepwise,
    StepwiseForked,
    StepwiseFurcated,
}

public static class AlgorithmUtils
{
    public static SolverSolution Search(this SolverConfig me, SimulationInput input, Action<ActionType>? actionCallback = null, CancellationToken token = default) =>
        Search(me, new SimulationState(input), actionCallback, token);

    public static SolverSolution Search(this SolverConfig me, SimulationState state, Action<ActionType>? actionCallback = null, CancellationToken token = default) =>
        Search(me.Algorithm, me, state, actionCallback, token);

    public static SolverSolution Search(this AlgorithmType me, SolverConfig config, SimulationInput input, Action<ActionType>? actionCallback = null, CancellationToken token = default) =>
        Search(me, config, new SimulationState(input), actionCallback, token);

    public static SolverSolution Search(this AlgorithmType me, SolverConfig config, SimulationState state, Action<ActionType>? actionCallback = null, CancellationToken token = default)
    {
        Func<SolverConfig, SimulationState, Action<ActionType>?, CancellationToken, SolverSolution> func = config.Algorithm switch
        {
            AlgorithmType.Oneshot => Oneshot.Search,
            AlgorithmType.OneshotForked => OneshotForked.Search,
            AlgorithmType.Stepwise => Stepwise.Search,
            AlgorithmType.StepwiseForked => StepwiseForked.Search,
            AlgorithmType.StepwiseFurcated or _ => StepwiseFurcated.Search,
        };
        return func(config, state, actionCallback, token);
    }
}
