using Craftimizer.Simulator;
using Craftimizer.Simulator.Actions;

namespace Craftimizer.Solver.Algorithms;

internal sealed class OneshotForked : IAlgorithm
{
    public static SolverSolution Search(SolverConfig config, SimulationState state, Action<ActionType>? actionCallback, CancellationToken token)
    {
        var tasks = new Task<(float MaxScore, SolverSolution Solution)>[config.ForkCount];
        for (var i = 0; i < config.ForkCount; ++i)
            tasks[i] = Task.Run(() =>
            {
                var solver = new Solver(config, state);
                solver.Search(config.Iterations / config.ForkCount, token);
                return (solver.MaxScore, solver.Solution());
            }, token);
        Task.WaitAll(tasks, CancellationToken.None);

        var solution = tasks.Select(t => t.Result).MaxBy(r => r.MaxScore).Solution;
        foreach (var action in solution.Actions)
            actionCallback?.Invoke(action);

        return solution;
    }
}
