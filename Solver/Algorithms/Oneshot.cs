using Craftimizer.Simulator;
using Craftimizer.Simulator.Actions;

namespace Craftimizer.Solver.Algorithms;

internal sealed class Oneshot : IAlgorithm
{
    public static SolverSolution Search(SolverConfig config, SimulationState state, Action<ActionType>? actionCallback, CancellationToken token)
    {
        var solver = new Solver(config, state);
        solver.Search(config.Iterations, token);
        var solution = solver.Solution();
        foreach (var action in solution.Actions)
            actionCallback?.Invoke(action);

        return solution;
    }
}
