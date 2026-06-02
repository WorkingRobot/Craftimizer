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

    // When a wall-clock budget is active (NextActionForked with MaxTimeMs > 0), progress is measured
    // in elapsed milliseconds against the time budget rather than iterations — the iteration count is
    // unbounded/huge under a time cap and would peg the bar at 100% instantly.
    private Stopwatch? progressTimer;
    private bool timeProgress;

    // In iterative algorithms, the value can be reset back to 0 (and progress stage increases by 1)
    // In other algorithms, the value increases monotonically.
    public int ProgressValue => timeProgress && progressTimer is { } t ? Math.Min((int)t.ElapsedMilliseconds, maxProgress) : progress;
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

    // True when ProgressValue/ProgressMax are wall-clock milliseconds (a time-budgeted solve) rather
    // than iteration counts, so the UI can label the progress bar appropriately.
    public bool IsTimeLimited => timeProgress;

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
            SolverAlgorithm.NextActionForked => (SearchNextActionForked, false),
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

        maxProgress = 50000;

        var s = new SimulatorNoRandom() { State = State };
        var pool = RaphaelUtils.ConvertToRawActions(Config.ActionPool.Where(a => a.Base().IsPossible(s)).ToArray());
        var input = new Raphael.SolverInput()
        {
            CP = checked((ushort)State.Input.Stats.CP),
            Durability = checked((ushort)State.Input.Recipe.MaxDurability),
            Progress = checked((ushort)State.Input.Recipe.MaxProgress),
            Quality = checked((ushort)(State.Input.Recipe.MaxQuality - State.Input.StartingQuality)),
            BaseProgressGain = checked((ushort)State.Input.BaseProgressGain),
            BaseQualityGain = checked((ushort)State.Input.BaseQualityGain),
            JobLevel = checked((byte)State.Input.Stats.Level),
            StellarSteadyHandCharges = 0,
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

        void Log(string s) =>
            OnLog?.Invoke(s);

        Raphael.SolverConfig config = new()
        {
            Adversarial = Config.Adversarial,
            BackloadProgress = Config.BackloadProgress,
            AllowNonMaxQualitySolutions = true,
            LogLevel = Raphael.LevelFilter.Debug,
            ThreadCount = (ushort)Config.MaxThreadCount,
        };

        using var solver = new Raphael.Solver(in config, in input, pool);

        solver.OnFinish += OnFinish;
        solver.OnSuggestSolution += OnSuggestSolution;
        solver.OnProgress += OnProgress;
        solver.OnLog += Log;

        progressStage = 0;
        progress = 0;
        await using var registration = Token.Register(solver.Cancel).ConfigureAwait(true);
        await Task.Run(solver.Solve, Token).ConfigureAwait(true);
        Token.ThrowIfCancellationRequested();

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
                // Ensures thread safety
                var forkRng = new Random(State.Input.Random.Next());
                tasks[i] = Task.Run(async () =>
                    {
                        var solver = new MCTS(MCTSConfig, activeStates[stateIdx].State, forkRng);
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
            {
                // Ensures thread safety
                var forkRng = new Random(State.Input.Random.Next());
                tasks[i] = Task.Run(async () =>
                {
                    var solver = new MCTS(MCTSConfig, state, forkRng);
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
            }
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

            var solver = new MCTS(MCTSConfig, state, state.Input.Random);

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
        {
            // Ensures thread safety
            var forkRng = new Random(State.Input.Random.Next());
            tasks[i] = Task.Run(async () =>
            {
                var solver = new MCTS(MCTSConfig, State, forkRng);
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
        }
        semaphore.Release(Config.MaxThreadCount);
        await Task.WhenAll(tasks).WaitAsync(Token).ConfigureAwait(false);
        s.Stop();
        OnLog?.Invoke($"{s.Elapsed.TotalMilliseconds:0.00}ms {(float)progress / Config.ForkCount / s.Elapsed.TotalSeconds / 1000:0.00} kI/s/t");

        var solution = tasks.Select(t => t.Result).MaxBy(r => r.MaxScore).Solution;
        foreach (var action in solution.Actions)
            InvokeNewAction(action);

        return solution;
    }

    // Greedy redundancy trim: drop any action whose removal still completes the craft at >= the same
    // quality. Random-rollout rotations ramble (padding + inefficient steps); this removes provably
    // redundant actions without ever reducing quality, shortening the macro and tightening the
    // (quality, steps) estimate used to rank candidates. Deterministic, so it also stabilizes output.
    private (List<ActionType> Actions, SimulationState State) TrimRotation(SimulationState initial, List<ActionType> actions, int progressTarget)
    {
        var sim = new Simulator(Config.ActionPool, Config.MaxStepCount);
        var start = initial;

        SimulationState Replay(List<ActionType> seq, out bool complete)
        {
            var state = start;
            foreach (var act in seq)
            {
                var (resp, next) = sim.Execute(state, act);
                if (resp == ActionResponse.UsedAction)
                    state = next;
            }
            complete = state.Progress >= progressTarget;
            return state;
        }

        var current = actions;
        var baseState = Replay(current, out _);
        var baseQuality = baseState.Quality;

        var improved = true;
        while (improved)
        {
            improved = false;
            for (var i = 0; i < current.Count; i++)
            {
                var trial = new List<ActionType>(current.Count - 1);
                for (var j = 0; j < current.Count; j++)
                    if (j != i)
                        trial.Add(current[j]);
                var trialState = Replay(trial, out var trialComplete);
                if (trialComplete && trialState.Quality >= baseQuality)
                {
                    current = trial;
                    improved = true;
                    break;
                }
            }
        }

        return (current, Replay(current, out _));
    }

    // Latency-bounded "best next action" solver: spend the whole iteration budget ranking the
    // immediate actions. For each candidate next action, run one MCTS (with best-solution tracking)
    // from the post-action state to estimate the best achievable final rotation, and pick the
    // candidate that leads to the best (completed, highest quality, fewest steps). Budget is split
    // across candidates and run across MaxThreadCount cores; the Token caps wall-clock and each MCTS
    // tolerates cancellation by returning its best-so-far. Concentrates compute on the single
    // decision rather than furcating toward full rotations.
    private Task<SolverSolution> SearchNextActionForked()
    {
        maxProgress = Config.Iterations;

        var rootSim = new Simulator(Config.ActionPool, Config.MaxStepCount, State);
        if (rootSim.IsComplete)
            return Task.FromResult(new SolverSolution(Array.Empty<ActionType>(), State));

        var candidateSet = rootSim.AvailableActionsHeuristic(Config.StrictActions);
        var n = candidateSet.Count;
        if (n == 0)
            return Task.FromResult(new SolverSolution(Array.Empty<ActionType>(), State));

        var candidates = new ActionType[n];
        var candidateSeeds = new int[n];
        for (var i = 0; i < n; i++)
        {
            candidates[i] = candidateSet.ElementAt(i);
            // Draw per-candidate seeds sequentially from the shared RNG (race-free + reproducible);
            // each candidate's MCTS then uses an independent Random.
            candidateSeeds[i] = State.Input.Random.Next();
        }

        // Budget: either a wall-clock time slice per candidate (MaxTimeMs > 0) or a fixed iteration
        // budget split across candidates. With a time budget, candidates run in ceil(n / parallelism)
        // sequential waves (parallelism = min(cores, n)); giving each candidate a slice of
        // MaxTimeMs / waveCount makes the whole decision land in ~MaxTimeMs total, regardless of how
        // many cores vs candidates there are (a single wave of few candidates each gets the full
        // budget; many candidates on few cores each get a proportionally smaller slice).
        var useTime = Config.MaxTimeMs > 0;
        var parallelism = Math.Min(Math.Max(1, Config.MaxThreadCount), n);
        var waveCount = (n + parallelism - 1) / parallelism;
        var sliceMs = useTime ? Math.Max(1, Config.MaxTimeMs / waveCount) : 0;
        if (useTime)
        {
            // Drive the progress bar off the wall-clock budget rather than the (unbounded) iteration count.
            maxProgress = Config.MaxTimeMs;
            timeProgress = true;
        }
        var iterCount = useTime ? int.MaxValue : Math.Max(1, Config.Iterations / n);
        var maxIterCount = useTime ? int.MaxValue : Math.Max(iterCount, Math.Max(Config.Iterations, Config.MaxIterations) / n);
        var mctsConfig = MCTSConfig;
        var progressTarget = State.Input.Recipe.MaxProgress;

        // Candidate screening: when there are more candidates than PruneActionCount, screen them all
        // briefly then spend the rest of the budget deepening only the best PruneActionCount. With the
        // default (the core count) this only happens when actions outnumber cores, since otherwise
        // every candidate already gets the full budget in one wave. (Fall back to the core count if an
        // older saved config predates the field and left it at 0.)
        var screenFrac = Math.Clamp((Config.ScreenBudgetPercent <= 0 ? 33 : Config.ScreenBudgetPercent) / 100f, 0.05f, 0.95f);
        var topK = Math.Min(n, Config.PruneActionCount > 0 ? Config.PruneActionCount : Math.Max(1, Config.MaxThreadCount));
        var prune = topK < n;

        var results = new (bool Completed, int Quality, int Steps, SolverSolution Sol)[n];
        var mctsList = new MCTS?[n];

        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Max(1, Config.MaxThreadCount),
            CancellationToken = Token,
        };

        // Search candidate i for a budget (wall-clock ms when useTime, else iterations; maxBudget is
        // the iteration ceiling for the "no completion found yet" extension). The MCTS instance is
        // reused across calls and accumulates, so "screen then deepen" just keeps searching.
        void RunSearch(int i, int budget, int maxBudget)
        {
            if (mctsList[i] is not { } mcts)
                return;
            using var cts = useTime ? CancellationTokenSource.CreateLinkedTokenSource(Token) : null;
            cts?.CancelAfter(budget);
            try
            {
                mcts.Search(useTime ? int.MaxValue : budget, useTime ? int.MaxValue : maxBudget, ref progress, cts?.Token ?? Token);
            }
            catch (OperationCanceledException) { } // time slice / cancellation: keep best-so-far
        }

        // Cheap interim ranking key (no trim) — only used to decide which candidates to deepen.
        (bool Completed, int Quality, int Steps) Interim(int i)
        {
            if (mctsList[i] is { } mcts)
            {
                var sub = mcts.Solution();
                return (sub.State.Progress >= progressTarget, sub.State.Quality, sub.State.ActionCount);
            }
            var r = results[i];
            return r.Sol.Actions is null ? (false, -1, int.MaxValue) : (r.Completed, r.Quality, r.Steps);
        }

        // Final (trimmed) result for candidate i from its (possibly deepened) MCTS.
        void Finalize(int i)
        {
            if (mctsList[i] is not { } mcts)
                return; // terminal/invalid: result already stored (or left null = unusable)
            var sub = mcts.Solution();
            List<ActionType> rotation = [candidates[i], .. sub.Actions];
            var finalState = sub.State;
            if (rotation.Count > 1)
                (rotation, finalState) = TrimRotation(State, rotation, progressTarget);
            results[i] = (finalState.Progress >= progressTarget, finalState.Quality, finalState.ActionCount, new SolverSolution(rotation, finalState));
        }

        var s = Stopwatch.StartNew();
        progressTimer = s;
        try
        {
            // Setup: execute each candidate once. Immediate finishes get a final result; the rest get
            // a fresh (unsearched) MCTS to be searched below.
            Parallel.For(0, n, options, i =>
            {
                var (resp, after) = new Simulator(Config.ActionPool, Config.MaxStepCount).Execute(State, candidates[i]);
                if (resp != ActionResponse.UsedAction)
                    return;
                if (after.Progress >= progressTarget || after.ActionCount >= Config.MaxStepCount)
                    results[i] = (after.Progress >= progressTarget, after.Quality, after.ActionCount, new SolverSolution([candidates[i]], after));
                else
                    mctsList[i] = new MCTS(mctsConfig, after, new Random(candidateSeeds[i]));
            });

            if (!prune)
            {
                // Default: evaluate every candidate with the full per-candidate slice.
                Parallel.For(0, n, options, i => { RunSearch(i, useTime ? sliceMs : iterCount, useTime ? sliceMs : maxIterCount); Finalize(i); });
            }
            else
            {
                // Phase 1 — screen all candidates shallowly.
                var screenBudget = useTime
                    ? Math.Max(1, (int)(Config.MaxTimeMs * screenFrac) / waveCount)
                    : Math.Max(1, (int)(Config.Iterations * screenFrac) / n);
                Parallel.For(0, n, options, i => RunSearch(i, screenBudget, screenBudget));

                // Keep the top-K survivors by interim (completed, quality, fewest steps).
                var survivors = Enumerable.Range(0, n)
                    .Where(i => mctsList[i] is not null)
                    .Select(i => (i, key: Interim(i)))
                    .OrderByDescending(t => t.key.Completed)
                    .ThenByDescending(t => t.key.Quality)
                    .ThenBy(t => t.key.Steps)
                    .Take(topK)
                    .Select(t => t.i)
                    .ToArray();

                // Phase 2 — deepen just the survivors with the remaining budget.
                var deepParallelism = Math.Min(Math.Max(1, Config.MaxThreadCount), Math.Max(1, survivors.Length));
                var deepWaves = Math.Max(1, (survivors.Length + deepParallelism - 1) / deepParallelism);
                var deepBudget = useTime
                    ? Math.Max(1, (int)(Config.MaxTimeMs * (1 - screenFrac)) / deepWaves)
                    : Math.Max(1, (int)(Config.Iterations * (1 - screenFrac)) / Math.Max(1, survivors.Length));
                Parallel.ForEach(survivors, options, i => RunSearch(i, deepBudget, deepBudget));

                // Finalize every candidate we created an MCTS for (survivors are deeper; the rest keep
                // their screen-level rotation so a genuinely-best pruned candidate can still win).
                Parallel.For(0, n, options, Finalize);
            }
        }
        catch (OperationCanceledException) { }
        s.Stop();
        OnLog?.Invoke($"{s.Elapsed.TotalMilliseconds:0.00}ms, {n} candidates{(prune ? $" (top-{topK} of {n})" : "")}");

        // argmax: completed first, then highest quality, then fewest steps.
        var bestIdx = -1;
        for (var i = 0; i < n; i++)
        {
            if (results[i].Sol.Actions is null)
                continue;
            if (bestIdx < 0)
            {
                bestIdx = i;
                continue;
            }
            ref var a = ref results[i];
            ref var b = ref results[bestIdx];
            var better = a.Completed != b.Completed ? a.Completed
                : a.Quality != b.Quality ? a.Quality > b.Quality
                : a.Steps < b.Steps;
            if (better)
                bestIdx = i;
        }

        if (bestIdx < 0)
            return Task.FromResult(new SolverSolution(new[] { candidates[0] }, State));

        var best = results[bestIdx].Sol;
        foreach (var act in best.Actions)
            InvokeNewAction(act);
        return Task.FromResult(best);
    }

    private Task<SolverSolution> SearchOneshot()
    {
        maxProgress = Config.Iterations;

        var solver = new MCTS(MCTSConfig, State, State.Input.Random);

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
