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
        if (args.Length > 0 && args[0] == "quality")
        {
            // Stochastic results+speed harness for the REAL (random-rollout) path.
            // usage: quality <nSeeds> <iters> [maxSteps]
            // Per recipe: mean/median/stdev MaxScore, mean Quality/MaxQuality, mean ms/search.
            var nSeeds = args.Length > 1 ? int.Parse(args[1]) : 32;
            var iters = args.Length > 2 ? int.Parse(args[2]) : 30_000;
            var maxStepsArg = args.Length > 3 ? int.Parse(args[3]) : -1;
            var baseCfg = Bench.Configs.First().Data;

            foreach (var stateWrap in Bench.States)
            {
                var recipe = stateWrap.Data.Input.Recipe;
                var stats = stateWrap.Data.Input.Stats;
                var cfgData = maxStepsArg > 0 ? baseCfg with { MaxStepCount = maxStepsArg } : baseCfg;
                var cfg = new MCTSConfig(cfgData);

                // warm up (JIT)
                for (var w = 0; w < 3; ++w)
                {
                    var ws = new MCTS(cfg, new SimulationState(new SimulationInput(stats, recipe, 0, 99999 + w)));
                    var wp = 0;
                    ws.Search(iters, iters, ref wp, CancellationToken.None);
                }

                var scores = new double[nSeeds];
                var quals = new double[nSeeds];
                var sw = Stopwatch.StartNew();
                for (var seed = 0; seed < nSeeds; ++seed)
                {
                    var solver = new MCTS(cfg, new SimulationState(new SimulationInput(stats, recipe, 0, seed)));
                    var progress = 0;
                    solver.Search(iters, iters, ref progress, CancellationToken.None);
                    var st = solver.Solution().State;
                    scores[seed] = solver.MaxScore;
                    quals[seed] = recipe.MaxQuality > 0 ? (double)st.Quality / recipe.MaxQuality : 1.0;
                }
                sw.Stop();

                var mean = scores.Average();
                var sorted = (double[])scores.Clone();
                Array.Sort(sorted);
                var median = sorted[nSeeds / 2];
                var variance = scores.Sum(s => (s - mean) * (s - mean)) / nSeeds;
                var stdev = Math.Sqrt(variance);
                Console.WriteLine($"recipe P{recipe.MaxProgress}Q{recipe.MaxQuality}D{recipe.MaxDurability}: " +
                    $"score mean={mean:F4} median={median:F4} stdev={stdev:F4} | qual mean={quals.Average():P1} | " +
                    $"{sw.Elapsed.TotalMilliseconds / nSeeds:F2}ms/search (n={nSeeds}, {iters} iters)");
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
            var solver = new MCTS(cfg, new SimulationState(seededInput));
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
                    var warm = new MCTS(cfg, initState0);
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
                    var solver = new MCTS(cfg, initState0);
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
                var solver = new MCTS(cfg, initState0);
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
            var solver = new MCTS(config, initState);
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
