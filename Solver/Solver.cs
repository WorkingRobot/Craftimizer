using Craftimizer.Simulator;
using Craftimizer.Simulator.Actions;
using System.Diagnostics;

namespace Craftimizer.Solver;

public static class Solver
{
    private static SolverSolution SearchStepwiseFurcated(SolverConfig config, SimulationState state, Action<ActionType>? actionCallback, CancellationToken token)
    {
        var definiteActionCount = 0;
        var bestSims = new List<(float Score, SolverSolution Result)>();

        var sim = new Simulator(state, config.MaxStepCount);

        var activeStates = new List<SolverSolution>() { new(new(), state) };

        while (activeStates.Count != 0)
        {
            if (token.IsCancellationRequested)
                break;

            var s = Stopwatch.StartNew();
            var tasks = new Task<(float MaxScore, int FurcatedActionIdx, SolverSolution Solution)>[config.ForkCount];
            for (var i = 0; i < config.ForkCount; i++)
            {
                var stateIdx = (int)((float)i / config.ForkCount * activeStates.Count);
                var st = activeStates[stateIdx];
                tasks[i] = Task.Run(() =>
                    {
                        var solver = new MCTS(new(config), activeStates[stateIdx].State);
                        solver.Search(config.Iterations / config.ForkCount, token);
                        return (solver.MaxScore, stateIdx, solver.Solution());
                    }, token);
            }
            Task.WaitAll(tasks, token);
            s.Stop();

            if (token.IsCancellationRequested)
                break;

            var bestActions = tasks.Select(t => t.Result).OrderByDescending(r => r.MaxScore).Take(config.FurcatedActionCount).ToArray();

            var bestAction = bestActions[0];
            if (bestAction.MaxScore >= config.ScoreStorageThreshold)
            {
                var (maxScore, furcatedActionIdx, solution) = bestAction;
                var (activeActions, activeState) = activeStates[furcatedActionIdx];

                activeActions.AddRange(solution.Actions);
                return solution with { Actions = activeActions };
            }

            var newStates = new List<SolverSolution>(config.FurcatedActionCount);
            for (var i = 0; i < bestActions.Length; ++i)
            {
                var (maxScore, furcatedActionIdx, (solutionActions, solutionNode)) = bestActions[i];
                if (solutionActions.Count == 0)
                    continue;

                var (activeActions, activeState) = activeStates[furcatedActionIdx];

                var chosenAction = solutionActions[0];

                var newActions = new List<ActionType>(activeActions) { chosenAction };
                var newState = sim.Execute(activeState, chosenAction).NewState;
                if (sim.IsComplete)
                    bestSims.Add((maxScore, new(newActions, newState)));
                else
                    newStates.Add(new(newActions, newState));
            }

            if (bestSims.Count == 0 && newStates.Count != 0)
            {
                var definiteCount = definiteActionCount;
                var equalCount = int.MaxValue;
                var refActions = newStates[0].Actions;
                for (var i = 1; i < newStates.Count; ++i)
                {
                    var cmpActions = newStates[i].Actions;
                    var possibleCount = Math.Min(Math.Min(refActions.Count, cmpActions.Count), equalCount);
                    var completelyEqual = true;
                    for (var j = definiteCount; j < possibleCount; ++j)
                    {
                        if (refActions[j] != cmpActions[j])
                        {
                            equalCount = j;
                            completelyEqual = false;
                            break;
                        }
                    }
                    if (completelyEqual)
                        equalCount = possibleCount;
                }
                if (definiteCount != equalCount)
                {
                    for (var i = definiteCount; i < equalCount; ++i)
                        actionCallback?.Invoke(refActions[i]);

                    definiteActionCount = equalCount;
                }
            }

            activeStates = newStates;

            Console.WriteLine($"{s.Elapsed.TotalMilliseconds:0.00}ms {config.Iterations / config.ForkCount / s.Elapsed.TotalSeconds / 1000:0.00} kI/s/t");
        }

        if (bestSims.Count == 0)
            return new(new(), state);

        var result = bestSims.MaxBy(s => s.Score).Result;
        for (var i = definiteActionCount; i < result.Actions.Count; ++i)
            actionCallback?.Invoke(result.Actions[i]);

        return result;
    }

    private static SolverSolution SearchStepwiseForked(SolverConfig config, SimulationState state, Action<ActionType>? actionCallback, CancellationToken token)
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
                    var solver = new MCTS(new(config), state);
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

    private static SolverSolution SearchStepwise(SolverConfig config, SimulationState state, Action<ActionType>? actionCallback, CancellationToken token)
    {
        var actions = new List<ActionType>();
        var sim = new Simulator(state, config.MaxStepCount);
        while (true)
        {
            if (token.IsCancellationRequested)
                break;

            if (sim.IsComplete)
                break;

            var solver = new MCTS(new(config), state);

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

    private static SolverSolution SearchOneshotForked(SolverConfig config, SimulationState state, Action<ActionType>? actionCallback, CancellationToken token)
    {
        var tasks = new Task<(float MaxScore, SolverSolution Solution)>[config.ForkCount];
        for (var i = 0; i < config.ForkCount; ++i)
            tasks[i] = Task.Run(() =>
            {
                var solver = new MCTS(new(config), state);
                solver.Search(config.Iterations / config.ForkCount, token);
                return (solver.MaxScore, solver.Solution());
            }, token);
        Task.WaitAll(tasks, CancellationToken.None);

        var solution = tasks.Select(t => t.Result).MaxBy(r => r.MaxScore).Solution;
        foreach (var action in solution.Actions)
            actionCallback?.Invoke(action);

        return solution;
    }

    private static SolverSolution SearchOneshot(SolverConfig config, SimulationState state, Action<ActionType>? actionCallback, CancellationToken token)
    {
        var solver = new MCTS(new(config), state);
        solver.Search(config.Iterations, token);
        var solution = solver.Solution();
        foreach (var action in solution.Actions)
            actionCallback?.Invoke(action);

        return solution;
    }

    public static SolverSolution Search(SolverConfig config, SimulationState state, Action<ActionType>? actionCallback, CancellationToken token)
    {
        Func<SolverConfig, SimulationState, Action<ActionType>?, CancellationToken, SolverSolution> func = config.Algorithm switch
        {
            SolverAlgorithm.Oneshot => SearchOneshot,
            SolverAlgorithm.OneshotForked => SearchOneshotForked,
            SolverAlgorithm.Stepwise => SearchStepwise,
            SolverAlgorithm.StepwiseForked => SearchStepwiseForked,
            SolverAlgorithm.StepwiseFurcated => SearchStepwiseFurcated,
            _ => throw new ArgumentOutOfRangeException(nameof(config), config, $"Invalid algorithm: {config.Algorithm}")
        };
        return func(config, state, actionCallback, token);
    }
}
