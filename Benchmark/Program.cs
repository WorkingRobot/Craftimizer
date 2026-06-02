using Craftimizer.Simulator;
using Craftimizer.Simulator.Actions;
using Craftimizer.Solver;
using ObjectLayoutInspector;
using System.Diagnostics;

namespace Craftimizer.Benchmark;

internal static class Program
{
    private static void Main(string[] args)
    {
        // Cross-implementation timing / correctness, available in any build configuration.
        // (In a non-deterministic build these exercise the real random-rollout path.)
        if (args.Length > 0 && args[0] == "nextaction")
        {
            // Synthesis-helper next-action eval: follow-the-solver (take next action, re-solve) from
            // scratch AND a mid-craft prefix of Raphael's optimal rotation; compare final quality to
            // Raphael's optimum. usage: nextaction <itersPerStep> <threads> [algos]
            var iters = args.Length > 1 ? int.Parse(args[1]) : 100_000;
            var threads = args.Length > 2 ? int.Parse(args[2]) : Environment.ProcessorCount;
            var nSeeds = args.Length > 3 ? int.Parse(args[3]) : 3;

            var panel = new List<(CharacterStats Stats, RecipeInfo Recipe, string Label)>();
            foreach (var sw0 in Bench.States)
                panel.Add((sw0.Data.Input.Stats, sw0.Data.Input.Recipe,
                    $"P{sw0.Data.Input.Recipe.MaxProgress}Q{sw0.Data.Input.Recipe.MaxQuality}D{sw0.Data.Input.Recipe.MaxDurability}"));
            var f0 = Bench.States.First().Data.Input;
            panel.Add((f0.Stats, f0.Recipe with { MaxDurability = 20 }, "lowDur(D20)"));

            // Follow-the-solver: repeatedly solve(state, algo, budget) -> take Actions[0] -> execute.
            static (int Quality, int Steps, bool Completed) Follow(
                CharacterStats stats, RecipeInfo recipe, SimulationState start,
                SolverAlgorithm algo, int iters, int threads)
            {
                var state = start;
                var sim = new SimulatorNoRandom();
                var maxStep = new SolverConfig().MaxStepCount;
                while (state.Progress < recipe.MaxProgress && state.ActionCount < maxStep)
                {
                    var cfg = new SolverConfig { Algorithm = algo, Iterations = iters, MaxThreadCount = threads, MaxTimeMs = int.TryParse(Environment.GetEnvironmentVariable("CRAFT_MAXMS"), out var mm) ? mm : 0 };
                    using var solver = new Solver.Solver(cfg, state);
                    solver.Start();
                    var sol = solver.GetTask().GetAwaiter().GetResult();
                    if (sol.Actions.Count == 0)
                        break;
                    var (resp, next) = sim.Execute(state, sol.Actions[0]);
                    if (resp != ActionResponse.UsedAction)
                        break;
                    state = next;
                }
                return (state.Quality, state.ActionCount, state.Progress >= recipe.MaxProgress);
            }

            foreach (var (stats, recipe, label) in panel)
            {
                // Raphael oracle: optimal rotation R + final quality Q*.
                IReadOnlyList<ActionType> R = Array.Empty<ActionType>();
                double qStar = double.NaN;
                try
                {
                    var rCfg = new SolverConfig { Algorithm = SolverAlgorithm.Raphael };
                    using var rSolver = new Solver.Solver(rCfg, new SimulationState(new SimulationInput(stats, recipe, 0, 0)));
                    rSolver.Start();
                    var rSol = rSolver.GetTask().GetAwaiter().GetResult();
                    R = rSol.Actions;
                    qStar = recipe.MaxQuality > 0 ? Math.Min(1.0, (double)rSol.State.Quality / recipe.MaxQuality) : 1.0;
                }
                catch (Exception e) { Console.WriteLine($"{label}: RAPHAEL failed: {e.Message}"); continue; }

                Console.WriteLine($"=== {label}  RAPHAEL qual={qStar:P1} steps={R.Count} ({iters}it/step, {threads}thr, n={nSeeds}) ===");
                // prefixes: scratch (k=0) and mid (k=R/2) of Raphael's optimal path.
                var algoFilter = args.Length > 4 ? args[4] : "both";
                var algos = algoFilter switch
                {
                    "n" => new[] { SolverAlgorithm.NextActionForked },
                    "g" => new[] { SolverAlgorithm.StepwiseGenetic },
                    _ => new[] { SolverAlgorithm.StepwiseGenetic, SolverAlgorithm.NextActionForked },
                };
                foreach (var k in new[] { 0, R.Count / 2 })
                {
                    foreach (var algo in algos)
                    {
                        double qSum = 0; var done = 0; double stepSum = 0; var stepMin = int.MaxValue; var stepMax = 0;
                        var sw = Stopwatch.StartNew();
                        for (var seed = 0; seed < nSeeds; ++seed)
                        {
                            var startSim = new SimulatorNoRandom();
                            var (_, sk, _) = startSim.ExecuteMultiple(new SimulationState(new SimulationInput(stats, recipe, 0, seed)), R.Take(k));
                            var (q, steps, c) = Follow(stats, recipe, sk, algo, iters, threads);
                            qSum += recipe.MaxQuality > 0 ? Math.Min(1.0, (double)q / recipe.MaxQuality) : 1.0;
                            stepSum += steps; stepMin = Math.Min(stepMin, steps); stepMax = Math.Max(stepMax, steps);
                            if (c) done++;
                        }
                        sw.Stop();
                        var qf = qSum / nSeeds;
                        Console.WriteLine($"  prefix k={k,2} {algo,-17}: qual={qf:P1} (gap={qStar - qf:P1}) steps~{stepSum / nSeeds:F1} [{stepMin}-{stepMax}] (raphael={R.Count}) complete={done}/{nSeeds} | {sw.Elapsed.TotalMilliseconds / nSeeds:F0}ms");
                    }
                }
            }
            return;
        }

        if (args.Length > 0 && args[0] == "rotation")
        {
            // FULL-rotation harness (the real plugin output): runs Stepwise to completion per seed
            // and reports the final rotation's metrics. This is where end-of-craft padding shows.
            // usage: rotation <nSeeds> <itersPerStep>
            var nSeeds = args.Length > 1 ? int.Parse(args[1]) : 16;
            var iters = args.Length > 2 ? int.Parse(args[2]) : 5000;

            var panel = new List<(CharacterStats Stats, RecipeInfo Recipe, string Label)>();
            foreach (var sw0 in Bench.States)
                panel.Add((sw0.Data.Input.Stats, sw0.Data.Input.Recipe,
                    $"P{sw0.Data.Input.Recipe.MaxProgress}Q{sw0.Data.Input.Recipe.MaxQuality}D{sw0.Data.Input.Recipe.MaxDurability}"));
            var first0 = Bench.States.First().Data.Input;
            panel.Add((first0.Stats, first0.Recipe with { MaxDurability = 20 }, "lowDur(D20)"));

            foreach (var (stats, recipe, label) in panel)
            {
                // Raphael oracle: the optimal quality/steps for this recipe (the target to approach).
                double rQual = double.NaN; int rSteps = 0;
                try
                {
                    var rInput = new SimulationInput(stats, recipe, 0, 0);
                    var rCfg = new SolverConfig { Algorithm = SolverAlgorithm.Raphael };
                    using var rSolver = new Solver.Solver(rCfg, new SimulationState(rInput));
                    rSolver.Start();
                    var rSt = rSolver.GetTask().GetAwaiter().GetResult().State;
                    rQual = recipe.MaxQuality > 0 ? Math.Min(1.0, (double)rSt.Quality / recipe.MaxQuality) : 1.0;
                    rSteps = rSt.ActionCount;
                }
                catch (Exception e) { Console.WriteLine($"{label,-16}: RAPHAEL failed: {e.GetType().Name} {e.Message}"); }

                double qualSum = 0, stepSum = 0, durSum = 0, cpSum = 0, completed = 0;
                // count trailing durability-restore actions (the #6/#44 padding signal)
                double trailingMends = 0;
                var sw = Stopwatch.StartNew();
                for (var seed = 0; seed < nSeeds; ++seed)
                {
                    var input = new SimulationInput(stats, recipe, 0, seed);
                    var algo = Environment.GetEnvironmentVariable("ROT_ALGO") switch
                    {
                        "genetic" => SolverAlgorithm.StepwiseGenetic,
                        "oneshot" => SolverAlgorithm.Oneshot,
                        "oneshotforked" => SolverAlgorithm.OneshotForked,
                        "stepwiseforked" => SolverAlgorithm.StepwiseForked,
                        _ => SolverAlgorithm.Stepwise,
                    };
                    var rotCfg = new SolverConfig { Algorithm = algo, Iterations = iters };
                    using var solver = new Solver.Solver(rotCfg, new SimulationState(input));
                    solver.Start();
                    var sol = solver.GetTask().GetAwaiter().GetResult();
                    var st = sol.State;
                    qualSum += recipe.MaxQuality > 0 ? Math.Min(1.0, (double)st.Quality / recipe.MaxQuality) : 1.0;
                    stepSum += st.ActionCount;
                    durSum += (double)st.Durability / recipe.MaxDurability;
                    cpSum += (double)st.CP / stats.CP;
                    if (st.Progress >= recipe.MaxProgress)
                        completed++;
                    // trailing durability-restore / non-quality padding actions at the end
                    for (var i = sol.Actions.Count - 1; i >= 0; --i)
                    {
                        var a = sol.Actions[i];
                        if (a is ActionType.MastersMend or ActionType.ImmaculateMend or ActionType.Manipulation
                            or ActionType.WasteNot or ActionType.WasteNot2 or ActionType.TrainedPerfection)
                            trailingMends++;
                        else
                            break;
                    }
                }
                sw.Stop();
                var mctsQual = qualSum / nSeeds;
                var gap = double.IsNaN(rQual) ? double.NaN : rQual - mctsQual;
                Console.WriteLine($"{label,-16}: MCTS qual={mctsQual:P1} steps={stepSum / nSeeds:F1} | " +
                    $"RAPHAEL qual={rQual:P1} steps={rSteps} | gap={gap:P1} | " +
                    $"leftDur={durSum / nSeeds:P0} leftCP={cpSum / nSeeds:P0} {sw.Elapsed.TotalMilliseconds / nSeeds:F0}ms (n={nSeeds},{iters}it)");
            }
            return;
        }

        if (args.Length > 0 && args[0] == "quality")
        {
            // Stochastic results+speed harness for the REAL (random-rollout) path.
            // usage: quality <nSeeds> <iters> [maxSteps]
            // Per recipe: quality%, completion%, mean steps, leftover dur%/cp% (padding signal), ms.
            var nSeeds = args.Length > 1 ? int.Parse(args[1]) : 32;
            var iters = args.Length > 2 ? int.Parse(args[2]) : 30_000;
            var maxStepsArg = args.Length > 3 ? int.Parse(args[3]) : -1;
            var baseCfg = Bench.Configs.First().Data;

            // Panel: the two bench recipes + a low-durability stress recipe (issue #42).
            var panel = new List<(CharacterStats Stats, RecipeInfo Recipe, string Label)>();
            foreach (var sw0 in Bench.States)
                panel.Add((sw0.Data.Input.Stats, sw0.Data.Input.Recipe,
                    $"P{sw0.Data.Input.Recipe.MaxProgress}Q{sw0.Data.Input.Recipe.MaxQuality}D{sw0.Data.Input.Recipe.MaxDurability}"));
            var first = Bench.States.First().Data.Input;
            panel.Add((first.Stats, first.Recipe with { MaxDurability = 20 }, "lowDur(D20)"));

            foreach (var (stats, recipe, label) in panel)
            {
                var cfgData = maxStepsArg > 0 ? baseCfg with { MaxStepCount = maxStepsArg } : baseCfg;
                var cfg = new MCTSConfig(cfgData);

                for (var w = 0; w < 3; ++w) // warm up (JIT)
                {
                    var wInput = new SimulationInput(stats, recipe, 0, 99999 + w);
                    var ws = new MCTS(cfg, new SimulationState(wInput), wInput.Random);
                    var wp = 0;
                    ws.Search(iters, iters, ref wp, CancellationToken.None);
                }

                double scoreSum = 0, qualSum = 0, stepSum = 0, durSum = 0, cpSum = 0, completed = 0;
                var sw = Stopwatch.StartNew();
                for (var seed = 0; seed < nSeeds; ++seed)
                {
                    var sInput = new SimulationInput(stats, recipe, 0, seed);
                    var solver = new MCTS(cfg, new SimulationState(sInput), sInput.Random);
                    var progress = 0;
                    solver.Search(iters, iters, ref progress, CancellationToken.None);
                    var st = solver.Solution().State;
                    scoreSum += solver.MaxScore;
                    qualSum += recipe.MaxQuality > 0 ? Math.Min(1.0, (double)st.Quality / recipe.MaxQuality) : 1.0;
                    stepSum += st.ActionCount;
                    durSum += (double)st.Durability / recipe.MaxDurability;
                    cpSum += (double)st.CP / stats.CP;
                    if (st.Progress >= recipe.MaxProgress)
                        completed++;
                }
                sw.Stop();

                Console.WriteLine($"{label,-16}: qual={qualSum / nSeeds:P1} steps={stepSum / nSeeds:F1} " +
                    $"complete={completed / nSeeds:P0} leftoverDur={durSum / nSeeds:P0} leftoverCP={cpSum / nSeeds:P0} " +
                    $"| score={scoreSum / nSeeds:F4} | {sw.Elapsed.TotalMilliseconds / nSeeds:F1}ms (n={nSeeds},{iters}it)");
            }
            return;
        }

        if (args.Length > 0 && args[0] == "seedfp")
        {
            // Seeded correctness oracle for the REAL (random-rollout) path: with a fixed RNG
            // seed the whole search is reproducible, so output must be byte-identical before/after
            // a behaviour-preserving optimization. Run in a non-deterministic (Release) build.
            var seed = args.Length > 1 ? int.Parse(args[1]) : 12345;
            var iters = args.Length > 2 ? int.Parse(args[2]) : 30_000;
            var baseState = Bench.States.First().Data;
            var seededInput = new SimulationInput(baseState.Input.Stats, baseState.Input.Recipe, 0, seed);
            var cfg = new MCTSConfig(Bench.Configs.First().Data);
            var solver = new MCTS(cfg, new SimulationState(seededInput), seededInput.Random);
            var progress = 0;
            solver.Search(iters, iters, ref progress, CancellationToken.None);
            var sol = solver.Solution();
            var st = sol.State;
            Console.WriteLine($"seed={seed} MaxScore={solver.MaxScore:R}");
            Console.WriteLine($"Quality={st.Quality} Progress={st.Progress} Durability={st.Durability} CP={st.CP} Steps={st.StepCount}");
            Console.WriteLine($"Actions={string.Join(",", sol.Actions)}");
            return;
        }

        if (args.Length > 0 && (args[0] == "solve" || args[0] == "fingerprint"))
        {
            var initConfig0 = Bench.Configs.First();
            var initState0 = Bench.States.First();

            if (args[0] == "solve")
            {
                // Matched single-search timing for cross-implementation comparison.
                // usage: solve <iterations> <maxStepCount> <count>
                var iters = args.Length > 1 ? int.Parse(args[1]) : 30_000;
                var maxSteps = args.Length > 2 ? int.Parse(args[2]) : 30;
                var count = args.Length > 3 ? int.Parse(args[3]) : 100;
                var cfg = new MCTSConfig(initConfig0.Data with { Iterations = iters, MaxIterations = iters, MaxStepCount = maxSteps });

                for (var i = 0; i < 3; ++i) // warm up
                {
                    var warm = new MCTS(cfg, initState0, initState0.Data.Input.Random);
                    var p = 0;
                    warm.Search(iters, iters, ref p, CancellationToken.None);
                }

                var gc0 = GC.CollectionCount(0);
                var gc1 = GC.CollectionCount(1);
                var gc2 = GC.CollectionCount(2);
                var alloc0 = GC.GetTotalAllocatedBytes(precise: true);
                var sw = Stopwatch.StartNew();
                for (var i = 0; i < count; ++i)
                {
                    var solver = new MCTS(cfg, initState0, initState0.Data.Input.Random);
                    var progress = 0;
                    solver.Search(iters, iters, ref progress, CancellationToken.None);
                    _ = solver.Solution();
                }
                sw.Stop();
                var allocPerSearch = (GC.GetTotalAllocatedBytes(precise: true) - alloc0) / (double)count;
                Console.WriteLine($"{sw.Elapsed.TotalMilliseconds / count:0.000}ms/search ({iters} iters, maxSteps {maxSteps}, n={count})");
                Console.WriteLine($"  alloc/search={allocPerSearch / 1024:0.0}KB  GC gen0={GC.CollectionCount(0) - gc0} gen1={GC.CollectionCount(1) - gc1} gen2={GC.CollectionCount(2) - gc2} (over n={count})");
            }
            else
            {
                // Deterministic correctness oracle: solve once and dump a fingerprint so
                // optimizations can be verified to produce byte-identical solver output.
                var cfg = new MCTSConfig(initConfig0.Data);
                var solver = new MCTS(cfg, initState0, initState0.Data.Input.Random);
                var progress = 0;
                solver.Search(initConfig0.Data.Iterations, initConfig0.Data.MaxIterations, ref progress, CancellationToken.None);
                var solution = solver.Solution();
                var st = solution.State;
                Console.WriteLine($"MaxScore={solver.MaxScore:R}");
                Console.WriteLine($"Quality={st.Quality} Progress={st.Progress} Durability={st.Durability} CP={st.CP} Steps={st.StepCount} ActionCount={st.ActionCount}");
                Console.WriteLine($"Actions={string.Join(",", solution.Actions)}");
            }
            return;
        }

#if IS_DETERMINISTIC
        var b = new Bench();

        var initConfig = Bench.Configs.First();
        var initState = Bench.States.First();

        var config = new MCTSConfig(initConfig.Data);

        var s = Stopwatch.StartNew();
        for (var i = 0; i < 100; ++i)
        {
            var solver = new MCTS(config, initState, initState.Data.Input.Random);
            var progress = 0;
            solver.Search(initConfig.Data.Iterations, initConfig.Data.MaxIterations, ref progress, CancellationToken.None);
            var solution = solver.Solution();
            Console.WriteLine($"{i+1}");
        }
        s.Stop();
        Console.WriteLine($"{s.Elapsed.TotalMilliseconds:0.00}ms");
#else
        RunBench(args);
#endif

        // return RunOther();
    }

    private static void RunBench(string[] args)
    {
        Environment.SetEnvironmentVariable("IS_BENCH", "1");
        BenchmarkDotNet.Running.BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }

    private static async Task RunTrace()
    {
        var input = new SimulationInput(
            new()
            {
                Craftsmanship = 4041,
                Control = 3905,
                CP = 609,
                Level = 90,
                CanUseManipulation = true,
                HasSplendorousBuff = false,
                IsSpecialist = false,
            },
            new RecipeInfo()
            {
                IsExpert = false,
                ClassJobLevel = 90,
                ConditionsFlag = 15,
                MaxDurability = 70,
                MaxQuality = 14040,
                MaxProgress = 6600,
                QualityModifier = 70,
                QualityDivider = 115,
                ProgressModifier = 80,
                ProgressDivider = 130,
            }
        );
        var config = new SolverConfig()
        {
            Algorithm = SolverAlgorithm.Stepwise,
            Iterations = 30000,
            MaxStepCount = 25
        };
        var solver = new Solver.Solver(config, new(input));
        solver.OnNewAction += s => Console.WriteLine($">{s}");
        solver.Start();
        var (_, s) = await solver.GetTask().ConfigureAwait(false);
        Console.WriteLine($"Qual: {s.Quality}/{s.Input.Recipe.MaxQuality}");
    }

    private static async Task RunOther()
    {
        TypeLayout.PrintLayout<SimulationState>(true);
        TypeLayout.PrintLayout<Simulator.Simulator>(true);
        TypeLayout.PrintLayout<BaseAction>(true);
        TypeLayout.PrintLayout<SimulationNode>(true);
        return;

        var input = new SimulationInput(
            new CharacterStats
            {
                Craftsmanship = 4078,
                Control = 3897,
                CP = 704,
                Level = 90,
                CanUseManipulation = true,
                HasSplendorousBuff = false,
                IsSpecialist = false,
            },
            new RecipeInfo()
            {
                IsExpert = false,
                ClassJobLevel = 90,
                ConditionsFlag = 15,
                MaxDurability = 70,
                MaxQuality = 14040,
                MaxProgress = 6600,
                QualityModifier = 70,
                QualityDivider = 115,
                ProgressModifier = 80,
                ProgressDivider = 130,
            }
        );

        var config = new SolverConfig()
        {
            Iterations = 100_000,
            ForkCount = 32,
            FurcatedActionCount = 16,
            MaxStepCount = 30,
        };

        var sim = new SimulatorNoRandom();
        (_, var state) = sim.Execute(new(input), ActionType.MuscleMemory);
        (_, state) = sim.Execute(state, ActionType.PrudentTouch);
        //(_, state) = sim.Execute(state, ActionType.Manipulation);
        //(_, state) = sim.Execute(state, ActionType.Veneration);
        //(_, state) = sim.Execute(state, ActionType.WasteNot);
        //(_, state) = sim.Execute(state, ActionType.Groundwork);
        //(_, state) = sim.Execute(state, ActionType.Groundwork);
        //(_, state) = sim.Execute(state, ActionType.Groundwork);
        //(_, state) = sim.Execute(state, ActionType.Innovation);
        //(_, state) = sim.Execute(state, ActionType.PrudentTouch);
        //(_, state) = sim.Execute(state, ActionType.AdvancedTouchCombo);
        //(_, state) = sim.Execute(state, ActionType.Manipulation);
        //(_, state) = sim.Execute(state, ActionType.Innovation);
        //(_, state) = sim.Execute(state, ActionType.PrudentTouch);
        //(_, state) = sim.Execute(state, ActionType.AdvancedTouchCombo);
        //(_, state) = sim.Execute(state, ActionType.GreatStrides);
        //(_, state) = sim.Execute(state, ActionType.Innovation);
        //(_, state) = sim.Execute(state, ActionType.FocusedTouchCombo);
        //(_, state) = sim.Execute(state, ActionType.GreatStrides);
        //(_, state) = sim.Execute(state, ActionType.ByregotsBlessing);
        //(_, state) = sim.Execute(state, ActionType.CarefulSynthesis);
        //(_, state) = sim.Execute(state, ActionType.CarefulSynthesis);

        Console.WriteLine($"{state.Quality} {state.CP} {state.Progress} {state.Durability}");
        //return;
        var solver = new Solver.Solver(config, state);
        solver.OnLog += Console.WriteLine;
        solver.OnNewAction += s => Console.WriteLine(s);
        solver.Start();
        var (_, s) = await solver.GetTask().ConfigureAwait(false);
        Console.WriteLine($"Qual: {s.Quality}/{s.Input.Recipe.MaxQuality}");
    }
}
