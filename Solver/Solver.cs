using Craftimizer.Simulator;
using Craftimizer.Simulator.Actions;
using System.Diagnostics;

namespace Craftimizer.Solver;

public sealed class Solver : IDisposable
{
    public SolverConfig Config { get; }
    public SimulationState State { get; }
    public CancellationToken Token { get; init; }
    private SolverSolution? Solution { get; set; }
    public SolverSolution? SanitizedSolution => Solution.HasValue ? Solution.Value with { ActionEnumerable = Solution.Value.Actions.SelectMany(SolverSolution.SanitizeCombo) } : null;

    public bool IsStarted => CompletionTask != null;
    public bool IsCompletedSuccessfully => Solution.HasValue;
    public bool IsCompleted => CompletionTask?.IsCompleted ?? false;

    private Func<Task<SolverSolution>> SearchFunc { get; }
    private MCTSConfig MCTSConfig => new(Config);
    private Task? CompletionTask { get; set; }

    private int progress;
    private int maxProgress;
    private int progressStage;

    // In iterative algorithms, the value can be reset back to 0 (and progress stage increases by 1)
    // In other algorithms, the value increases monotonically.
    public int ProgressValue => progress;
    // Maximum ProgressValue value.
    public int ProgressMax => maxProgress;
    // Always increases by 1 when ProgressValue is reset. Set to null if the algorithm is not iterative.
    public int? ProgressStage
    {
        get
        {
            var stage = progressStage;
            return stage == -1 ? null : stage;
        }
    }

    public bool IsIndeterminate => progress == 0 && progressStage == 0;

    public delegate void LogDelegate(string text);
    public delegate void NewActionDelegate(ActionType action);
    public delegate void SolutionDelegate(SolverSolution solution);

    // Print to console or plugin log.
    public event LogDelegate? OnLog;

    // Display as notification.
    public event LogDelegate? OnWarn;

    // Always called when a new step is generated.
    public event NewActionDelegate? OnNewAction;

    // Called when the solver can provide a "probable" solution.
    // OnNewAction actions precede these proposed solutions. Purely visual.
    public event SolutionDelegate? OnSuggestSolution;

    public Solver(in SolverConfig config, in SimulationState state)
    {
        Config = config;
        State = state;

        (SearchFunc, var hasProgressStage) = ((Func<Task<SolverSolution>>, bool))(Config.Algorithm switch
        {
            SolverAlgorithm.Oneshot => (SearchOneshot, false),
            SolverAlgorithm.OneshotForked => (SearchOneshotForked, false),
            SolverAlgorithm.Stepwise => (SearchStepwise, true),
            SolverAlgorithm.StepwiseForked => (SearchStepwiseForked, true),
            SolverAlgorithm.StepwiseGenetic => (SearchStepwiseGenetic, true),
            SolverAlgorithm.Raphael => (SearchRaphael, true),
            _ => throw new ArgumentOutOfRangeException(nameof(config), config, $"Invalid algorithm: {config.Algorithm}")
        });

        progressStage = hasProgressStage ? 0 : -1;
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

        progress = 0;
        Solution = await SearchFunc().ConfigureAwait(false);
    }

    public async Task<SolverSolution> GetTask()
    {
        if (!IsStarted)
            throw new InvalidOperationException("Solver has not started.");

        await CompletionTask!.ConfigureAwait(false);

        return SanitizedSolution!.Value;
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

    private void InvokeNewAction(ActionType action)
    {
        foreach (var sanitizedAction in SolverSolution.SanitizeCombo(action))
            OnNewAction?.Invoke(sanitizedAction);
    }

    private void ResetProgress()
    {
        if (!ProgressStage.HasValue)
            throw new InvalidOperationException("Progress cannot be reset.");

        Interlocked.Exchange(ref progress, 0);
        Interlocked.Increment(ref progressStage);
    }

    private async Task<SolverSolution> SearchRaphael()
    {
        if (State.ActionCount > 0)
        {
            OnWarn?.Invoke("Optimal solver not support existing actions; falling back to Stepwise Genetic.");
            return await SearchStepwiseGenetic().ConfigureAwait(false);
        }

        maxProgress = 2000;

        var s = new SimulatorNoRandom() { State = State };
        var pool = RaphaelUtils.ConvertToRawActions(Config.ActionPool.Where(a => a.Base().IsPossible(s)).ToArray());
        var input = new Raphael.SolverInput()
        {
            CP = checked((short)State.Input.Stats.CP),
            Durability = checked((sbyte)State.Input.Recipe.MaxDurability),
            Progress = checked((ushort)State.Input.Recipe.MaxProgress),
            Quality = checked((ushort)(State.Input.Recipe.MaxQuality - State.Input.StartingQuality)),
            BaseProgressGain = checked((ushort)State.Input.BaseProgressGain),
            BaseQualityGain = checked((ushort)State.Input.BaseQualityGain),
            JobLevel = checked((byte)State.Input.Stats.Level),
        };

        SimulationState ExecuteActions(IEnumerable<ActionType> actions)
        {
            var sim = new SimulatorNoRandom();
            var (resp, outState, failedIdx) = sim.ExecuteMultiple(State, actions);
            if (resp != ActionResponse.SimulationComplete)
            {
                if (failedIdx != -1)
                    throw new ArgumentException($"Invalid state; simulation failed to execute solution: {string.Join(',', actions)}", nameof(actions));
            }
            return outState;
        }

        ActionType[]? solution = null;

        void OnFinish(Raphael.Action[] s) =>
            solution = RaphaelUtils.ConvertRawActions(s);

        void OnSuggestSolution(Raphael.Action[] s)
        {
            var steps = RaphaelUtils.ConvertRawActions(s);
            var outState = ExecuteActions(steps);
            this.OnSuggestSolution?.Invoke(new(steps, in outState));
        }

        void OnProgress(nuint p)
        {
            var prog = checked((int)p);
            var stage = prog / maxProgress;
            while (stage != progressStage)
                ResetProgress();
            progress = prog % maxProgress;
        }

        if (!Config.MinimizeSteps)
        {
            Raphael.SolverConfig config = new()
            {
                Adversarial = Config.Adversarial,
                BackloadProgress = true,
                UnsoundBranchPruning = true
            };

            using var solver = new Raphael.Solver(in config, in input, pool);

            solver.OnFinish += OnFinish;
            solver.OnSuggestSolution += OnSuggestSolution;
            solver.OnProgress += OnProgress;

            progressStage = 0;
            progress = 0;
            await using var registration = Token.Register(solver.Cancel).ConfigureAwait(true);
            await Task.Run(solver.Solve, Token).ConfigureAwait(true);
            Token.ThrowIfCancellationRequested();
        }

        var state = solution != null ? (SimulationState?)ExecuteActions(solution) : null;
        if (solution == null || state?.Quality != state?.Input.Recipe.MaxQuality)
        {
            Raphael.SolverConfig config = new()
            {
                Adversarial = Config.Adversarial,
                BackloadProgress = Config.BackloadProgress,
                UnsoundBranchPruning = false
            };

            using var solver = new Raphael.Solver(in config, in input, pool);

            solver.OnFinish += OnFinish;
            solver.OnSuggestSolution += OnSuggestSolution;
            solver.OnProgress += OnProgress;

            progressStage = 0;
            progress = 0;
            await using var registration = Token.Register(solver.Cancel).ConfigureAwait(true);
            await Task.Run(solver.Solve, Token).ConfigureAwait(true);
            Token.ThrowIfCancellationRequested();
        }

        if (solution == null)
            return new([], State);

        foreach (var action in solution)
            InvokeNewAction(action);

        var outState = ExecuteActions(solution);
        return new(solution, in outState);
    }

    private async Task<SolverSolution> SearchStepwiseGenetic()
    {
        var iterCount = Config.Iterations / Config.ForkCount;
        var maxIterCount = Math.Max(Config.Iterations, Config.MaxIterations) / Config.ForkCount;
        maxProgress = iterCount * Config.ForkCount;

        var definiteActionCount = 0;
        var bestSims = new List<(float Score, SolverSolution Result)>();

        var state = State;
        var sim = new Simulator(Config.ActionPool, Config.MaxStepCount);

        var activeStates = new List<SolverSolution>() { new(Array.Empty<ActionType>(), state) };

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
                            solver.Search(iterCount, maxIterCount, ref progress, Token);
                        }
                        finally
                        {
                            try
                            {
                                semaphore.Release();
                            }
                            catch (ObjectDisposedException) { }
                        }
                        var solution = solver.Solution();
                        return (solver.MaxScore, stateIdx, solution);
                    }, Token);
            }
            semaphore.Release(Config.MaxThreadCount);
            await Task.WhenAll(tasks).WaitAsync(Token).ConfigureAwait(false);
            s.Stop();
            OnLog?.Invoke($"{s.Elapsed.TotalMilliseconds:0.00}ms {(float)progress / Config.ForkCount / s.Elapsed.TotalSeconds / 1000:0.00} kI/s/t");

            Token.ThrowIfCancellationRequested();

            var bestActions = tasks.Select(t => t.Result).OrderByDescending(r => r.MaxScore).Take(Config.FurcatedActionCount).ToArray();

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
                    foreach (var action in refActions.Take(equalCount).Skip(definiteCount))
                        InvokeNewAction(action);

                    definiteActionCount = equalCount;
                }
            }

            ResetProgress();

            activeStates = newStates;
        }

        if (bestSims.Count == 0)
            return new(Array.Empty<ActionType>(), state);

        var result = bestSims.MaxBy(s => s.Score).Result;
        foreach (var action in result.Actions.Skip(definiteActionCount))
            InvokeNewAction(action);

        return result;
    }

    private async Task<SolverSolution> SearchStepwiseForked()
    {
        var iterCount = Config.Iterations / Config.ForkCount;
        var maxIterCount = Math.Max(Config.Iterations, Config.MaxIterations) / Config.ForkCount;
        maxProgress = iterCount * Config.ForkCount;

        var actions = new List<ActionType>();
        var state = State;
        var sim = new Simulator(Config.ActionPool, Config.MaxStepCount, state);
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
                        solver.Search(iterCount, maxIterCount, ref progress, Token);
                    }
                    finally
                    {
                        try
                        {
                            semaphore.Release();
                        }
                        catch (ObjectDisposedException) { }
                    }
                    var solution = solver.Solution();
                    return (solver.MaxScore, solution);
                }, Token);
            semaphore.Release(Config.MaxThreadCount);
            await Task.WhenAll(tasks).WaitAsync(Token).ConfigureAwait(false);
            s.Stop();
            OnLog?.Invoke($"{s.Elapsed.TotalMilliseconds:0.00}ms {(float)progress / Config.ForkCount / s.Elapsed.TotalSeconds / 1000:0.00} kI/s/t");

            Token.ThrowIfCancellationRequested();

            var (maxScore, solution) = tasks.Select(t => t.Result).MaxBy(r => r.MaxScore);

            var chosenAction = solution.Actions[0];
            InvokeNewAction(chosenAction);

            (_, state) = sim.Execute(state, chosenAction);
            actions.Add(chosenAction);

            ResetProgress();
        }

        return new(actions, state);
    }

    private Task<SolverSolution> SearchStepwise()
    {
        maxProgress = Config.Iterations;

        var actions = new List<ActionType>();
        var state = State;
        var sim = new Simulator(Config.ActionPool, Config.MaxStepCount, state);
        while (true)
        {
            Token.ThrowIfCancellationRequested();

            if (sim.IsComplete)
                break;

            var solver = new MCTS(MCTSConfig, state);

            var s = Stopwatch.StartNew();
            solver.Search(Config.Iterations, Config.MaxIterations, ref progress, Token);
            s.Stop();
            OnLog?.Invoke($"{s.Elapsed.TotalMilliseconds:0.00}ms {progress / s.Elapsed.TotalSeconds / 1000:0.00} kI/s");

            var solution = solver.Solution();

            var chosenAction = solution.Actions[0];
            InvokeNewAction(chosenAction);

            (_, state) = sim.Execute(state, chosenAction);
            actions.Add(chosenAction);

            ResetProgress();
        }

        return Task.FromResult(new SolverSolution(actions, state));
    }

    private async Task<SolverSolution> SearchOneshotForked()
    {
        var iterCount = Config.Iterations / Config.ForkCount;
        var maxIterCount = Math.Max(Config.Iterations, Config.MaxIterations) / Config.ForkCount;
        maxProgress = iterCount * Config.ForkCount;

        using var semaphore = new SemaphoreSlim(0, Config.MaxThreadCount);
        var s = Stopwatch.StartNew();
        var tasks = new Task<(float MaxScore, SolverSolution Solution)>[Config.ForkCount];
        for (var i = 0; i < Config.ForkCount; ++i)
            tasks[i] = Task.Run(async () =>
            {
                var solver = new MCTS(MCTSConfig, State);
                await semaphore.WaitAsync(Token).ConfigureAwait(false);
                try
                {
                    solver.Search(iterCount, maxIterCount, ref progress, Token);
                }
                finally
                {
                    try
                    {
                        semaphore.Release();
                    }
                    catch (ObjectDisposedException) { }
                }
                var solution = solver.Solution();
                return (solver.MaxScore, solution);
            }, Token);
        semaphore.Release(Config.MaxThreadCount);
        await Task.WhenAll(tasks).WaitAsync(Token).ConfigureAwait(false);
        s.Stop();
        OnLog?.Invoke($"{s.Elapsed.TotalMilliseconds:0.00}ms {(float)progress / Config.ForkCount / s.Elapsed.TotalSeconds / 1000:0.00} kI/s/t");

        var solution = tasks.Select(t => t.Result).MaxBy(r => r.MaxScore).Solution;
        foreach (var action in solution.Actions)
            InvokeNewAction(action);

        return solution;
    }

    private Task<SolverSolution> SearchOneshot()
    {
        maxProgress = Config.Iterations;

        var solver = new MCTS(MCTSConfig, State);

        var s = Stopwatch.StartNew();
        solver.Search(Config.Iterations, Config.MaxIterations, ref progress, Token);
        s.Stop();
        OnLog?.Invoke($"{s.Elapsed.TotalMilliseconds:0.00}ms {progress / s.Elapsed.TotalSeconds / 1000:0.00} kI/s");

        var solution = solver.Solution();
        foreach (var action in solution.Actions)
            InvokeNewAction(action);

        return Task.FromResult(solution);
    }
}
