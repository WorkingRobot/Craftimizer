using Craftimizer.Simulator;
using Craftimizer.Simulator.Actions;
using System.Diagnostics;

namespace Craftimizer.Solver.Algorithms;

internal sealed class StepwiseForked : IAlgorithm
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


            var s = Stopwatch.StartNew();
            var tasks = new Task<(float MaxScore, SolverSolution Solution)>[config.ForkCount];
            for (var i = 0; i < config.ForkCount; ++i)
                tasks[i] = Task.Run(() =>
                {
                    var solver = new Solver(config, state);
                    solver.Search(config.Iterations / config.ForkCount, token);
                    return (solver.MaxScore, solver.Solution());
                }, token);
            Task.WaitAll(tasks, token);
            s.Stop();

            if (token.IsCancellationRequested)
                break;

            var (maxScore, solution) = tasks.Select(t => t.Result).MaxBy(r => r.MaxScore);

            if (maxScore >= config.ScoreStorageThreshold)
            {
                actions.AddRange(solution.Actions);
                return solution with { Actions = actions };
            }

            var chosenAction = solution.Actions[0];
            actionCallback?.Invoke(chosenAction);
            Console.WriteLine($"{s.Elapsed.TotalMilliseconds:0.00}ms {config.Iterations / config.ForkCount / s.Elapsed.TotalSeconds / 1000:0.00} kI/s/t");

            (_, state) = sim.Execute(state, chosenAction);
            actions.Add(chosenAction);
        }

        return new(actions, state);
    }
}
