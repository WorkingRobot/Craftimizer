using Craftimizer.Simulator;
using Craftimizer.Simulator.Actions;
using System.Diagnostics;

namespace Craftimizer.Solver;

public sealed class Solver : IDisposable
{
    public SolverConfig Config { get; }
    public SimulationState State { get; }
    public CancellationToken Token { get; init; }
    public SolverSolution? Solution { get; private set; }

    public bool IsStarted => CompletionTask != null;
    public bool IsCompletedSuccessfully => Solution != null;
    public bool IsCompleted => CompletionTask?.IsCompleted ?? false;

    private Func<Task<SolverSolution>> SearchFunc { get; }
    private MCTSConfig MCTSConfig => new(Config);
    private Task? CompletionTask { get; set; }

    public delegate void LogDelegate(string text);
    public delegate void WorkerProgressDelegate(SolverSolution solution, float score);
    public delegate void NewActionDelegate(ActionType action);
    public delegate void SolutionDelegate(SolverSolution solution);

    // Print to console or plugin log.
    public event LogDelegate? OnLog;

    // Isn't always called. This is just meant to show as an indicator to the user.
    // Solution contains the best terminal state, and its actions to get there exclude the ones provided by OnNewAction.
    // For example, to get to the terminal state, execute all OnNewAction actions, then execute all Solution actions.
    public event WorkerProgressDelegate? OnWorkerProgress;

    // Always called when a new step is generated.
    public event NewActionDelegate? OnNewAction;

    // Always called when the solver is fully complete.
    public event SolutionDelegate? OnSolution;

    public Solver(SolverConfig config, SimulationState state)
    {
        Config = config;
        State = state;

        SearchFunc = Config.Algorithm switch
        {
            SolverAlgorithm.Oneshot => SearchOneshot,
            SolverAlgorithm.OneshotForked => SearchOneshotForked,
            SolverAlgorithm.Stepwise => SearchStepwise,
            SolverAlgorithm.StepwiseForked => SearchStepwiseForked,
            SolverAlgorithm.StepwiseFurcated => SearchStepwiseFurcated,
            _ => throw new ArgumentOutOfRangeException(nameof(config), config, $"Invalid algorithm: {config.Algorithm}")
        };
    }

    public void Start()
    {
        if (IsStarted)
            throw new InvalidOperationException("Solver has already started.");

        CompletionTask = RunTask();
    }

    private async Task RunTask()
    {
        Token.ThrowIfCancellationRequested();

        Solution = await SearchFunc().ConfigureAwait(false);
    }

    public async Task<SolverSolution> GetTask()
    {
        if (!IsStarted)
            throw new InvalidOperationException("Solver has not started.");

        await CompletionTask!.ConfigureAwait(false);

        return Solution!.Value;
    }

    public async Task<SolverSolution?> GetSafeTask()
    {
        try
        {
            return await GetTask().ConfigureAwait(false);
        }
        catch (AggregateException e)
        {
            e.Flatten().Handle(ex => ex is OperationCanceledException);
        }
        catch (OperationCanceledException)
        {

        }
        return null;
    }

    public void TryWait()
    {
        if (IsStarted && !IsCompleted)
            GetSafeTask().Wait();
    }

    public void Dispose()
    {
        CompletionTask?.Dispose();
    }

    private async Task<SolverSolution> SearchStepwiseFurcated()
    {
        var definiteActionCount = 0;
        var bestSims = new List<(float Score, SolverSolution Result)>();

        var state = State;
        var sim = new Simulator(state, Config.MaxStepCount);

        var activeStates = new List<SolverSolution>() { new(new(), state) };

        while (activeStates.Count != 0)
        {
            Token.ThrowIfCancellationRequested();

            using var semaphore = new SemaphoreSlim(0, Config.MaxThreadCount);
            var s = Stopwatch.StartNew();
            var tasks = new Task<(float MaxScore, int FurcatedActionIdx, SolverSolution Solution)>[Config.ForkCount];
            for (var i = 0; i < Config.ForkCount; i++)
            {
                var stateIdx = (int)((float)i / Config.ForkCount * activeStates.Count);
                tasks[i] = Task.Run(async () =>
                    {
                        var solver = new MCTS(MCTSConfig, activeStates[stateIdx].State);
                        await semaphore.WaitAsync(Token).ConfigureAwait(false);
                        try
                        {
                            solver.Search(Config.Iterations / Config.ForkCount, Token);
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                        var solution = solver.Solution();
                        var progressActions = activeStates[stateIdx].Actions.Concat(solution.Actions).Skip(definiteActionCount).ToList();
                        OnWorkerProgress?.Invoke(solution with { Actions = progressActions }, solver.MaxScore);
                        return (solver.MaxScore, stateIdx, solution);
                    }, Token);
            }
            semaphore.Release(Config.MaxThreadCount);
            await Task.WhenAll(tasks).WaitAsync(Token).ConfigureAwait(false);
            s.Stop();
            OnLog?.Invoke($"{s.Elapsed.TotalMilliseconds:0.00}ms {Config.Iterations / Config.ForkCount / s.Elapsed.TotalSeconds / 1000:0.00} kI/s/t");

            Token.ThrowIfCancellationRequested();

            var bestActions = tasks.Select(t => t.Result).OrderByDescending(r => r.MaxScore).Take(Config.FurcatedActionCount).ToArray();

            var bestAction = bestActions[0];
            if (bestAction.MaxScore >= Config.ScoreStorageThreshold)
            {
                var (_, furcatedActionIdx, solution) = bestAction;
                var (activeActions, _) = activeStates[furcatedActionIdx];

                activeActions.AddRange(solution.Actions);
                foreach (var action in activeActions.Skip(definiteActionCount))
                    OnNewAction?.Invoke(action);
                return solution with { Actions = activeActions };
            }

            var newStates = new List<SolverSolution>(Config.FurcatedActionCount);
            for (var i = 0; i < bestActions.Length; ++i)
            {
                var (maxScore, furcatedActionIdx, (solutionActions, _)) = bestActions[i];
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
                    foreach(var action in refActions.Take(equalCount).Skip(definiteCount))
                        OnNewAction?.Invoke(action);

                    definiteActionCount = equalCount;
                }
            }

            activeStates = newStates;
        }

        if (bestSims.Count == 0)
            return new(new(), state);

        var result = bestSims.MaxBy(s => s.Score).Result;
        foreach (var action in result.Actions.Skip(definiteActionCount))
            OnNewAction?.Invoke(action);

        return result;
    }

    private async Task<SolverSolution> SearchStepwiseForked()
    {
        var actions = new List<ActionType>();
        var state = State;
        var sim = new Simulator(state, Config.MaxStepCount);
        while (true)
        {
            Token.ThrowIfCancellationRequested();

            if (sim.IsComplete)
                break;

            using var semaphore = new SemaphoreSlim(0, Config.MaxThreadCount);
            var s = Stopwatch.StartNew();
            var tasks = new Task<(float MaxScore, SolverSolution Solution)>[Config.ForkCount];
            for (var i = 0; i < Config.ForkCount; ++i)
                tasks[i] = Task.Run(async () =>
                {
                    var solver = new MCTS(MCTSConfig, state);
                    await semaphore.WaitAsync(Token).ConfigureAwait(false);
                    try
                    {
                        solver.Search(Config.Iterations / Config.ForkCount, Token);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                    var solution = solver.Solution();
                    OnWorkerProgress?.Invoke(solution, solver.MaxScore);
                    return (solver.MaxScore, solution);
                }, Token);
            semaphore.Release(Config.MaxThreadCount);
            await Task.WhenAll(tasks).WaitAsync(Token).ConfigureAwait(false);
            s.Stop();
            OnLog?.Invoke($"{s.Elapsed.TotalMilliseconds:0.00}ms {Config.Iterations / Config.ForkCount / s.Elapsed.TotalSeconds / 1000:0.00} kI/s/t");

            Token.ThrowIfCancellationRequested();

            var (maxScore, solution) = tasks.Select(t => t.Result).MaxBy(r => r.MaxScore);

            if (maxScore >= Config.ScoreStorageThreshold)
            {
                actions.AddRange(solution.Actions);
                foreach (var action in solution.Actions)
                    OnNewAction?.Invoke(action);
                return solution with { Actions = actions };
            }

            var chosenAction = solution.Actions[0];
            OnNewAction?.Invoke(chosenAction);

            (_, state) = sim.Execute(state, chosenAction);
            actions.Add(chosenAction);
        }

        return new(actions, state);
    }

    private Task<SolverSolution> SearchStepwise()
    {
        var actions = new List<ActionType>();
        var state = State;
        var sim = new Simulator(state, Config.MaxStepCount);
        while (true)
        {
            Token.ThrowIfCancellationRequested();

            if (sim.IsComplete)
                break;

            var solver = new MCTS(MCTSConfig, State);

            var s = Stopwatch.StartNew();
            solver.Search(Config.Iterations, Token);
            s.Stop();
            OnLog?.Invoke($"{s.Elapsed.TotalMilliseconds:0.00}ms {Config.Iterations / s.Elapsed.TotalSeconds / 1000:0.00} kI/s");

            var solution = solver.Solution();

            if (solver.MaxScore >= Config.ScoreStorageThreshold)
            {
                actions.AddRange(solution.Actions);
                foreach (var action in solution.Actions)
                    OnNewAction?.Invoke(action);
                return Task.FromResult(solution with { Actions = actions });
            }

            var chosenAction = solution.Actions[0];
            OnNewAction?.Invoke(chosenAction);

            (_, state) = sim.Execute(state, chosenAction);
            actions.Add(chosenAction);
        }

        return Task.FromResult(new SolverSolution(actions, state));
    }

    private async Task<SolverSolution> SearchOneshotForked()
    {
        using var semaphore = new SemaphoreSlim(0, Config.MaxThreadCount);
        var tasks = new Task<(float MaxScore, SolverSolution Solution)>[Config.ForkCount];
        for (var i = 0; i < Config.ForkCount; ++i)
            tasks[i] = Task.Run(async () =>
            {
                var solver = new MCTS(MCTSConfig, State);
                await semaphore.WaitAsync(Token).ConfigureAwait(false);
                try
                {
                    solver.Search(Config.Iterations / Config.ForkCount, Token);
                }
                finally
                {
                    semaphore.Release();
                }
                var solution = solver.Solution();
                OnWorkerProgress?.Invoke(solution, solver.MaxScore);
                return (solver.MaxScore, solution);
            }, Token);
        semaphore.Release(Config.MaxThreadCount);
        await Task.WhenAll(tasks).WaitAsync(Token).ConfigureAwait(false);

        var solution = tasks.Select(t => t.Result).MaxBy(r => r.MaxScore).Solution;
        foreach (var action in solution.Actions)
            OnNewAction?.Invoke(action);

        return solution;
    }

    private Task<SolverSolution> SearchOneshot()
    {
        var solver = new MCTS(MCTSConfig, State);
        solver.Search(Config.Iterations, Token);
        var solution = solver.Solution();
        foreach (var action in solution.Actions)
            OnNewAction?.Invoke(action);

        return Task.FromResult(solution);
    }
}
