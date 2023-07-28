using Craftimizer.Simulator;
using Craftimizer.Simulator.Actions;
using System.Diagnostics;

namespace Craftimizer.Solver.Algorithms;

internal sealed class Stepwise : IAlgorithm
{
    public static SolverSolution Search(SolverConfig config, SimulationState state, Action<ActionType>? actionCallback, CancellationToken token)
    {
        var actions = new List<ActionType>();
        var sim = new Simulator(state, config.MaxStepCount);
        while (true)
        {
            if (token.IsCancellationRequested)
                break;

            if (sim.IsComplete)
                break;

            var solver = new Solver(config, state);

            var s = Stopwatch.StartNew();
            solver.Search(config.Iterations, token);
            s.Stop();

            var solution = solver.Solution();

            if (solver.MaxScore >= config.ScoreStorageThreshold)
            {
                actions.AddRange(solution.Actions);
                return solution with { Actions = actions };
            }

            var chosenAction = solution.Actions[0];
            actionCallback?.Invoke(chosenAction);
            Console.WriteLine($"{s.Elapsed.TotalMilliseconds:0.00}ms {config.Iterations / s.Elapsed.TotalSeconds / 1000:0.00} kI/s");

            (_, state) = sim.Execute(state, chosenAction);
            actions.Add(chosenAction);
        }

        return new(actions, state);
    }
}
