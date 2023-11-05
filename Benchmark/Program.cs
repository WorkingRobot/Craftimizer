using Craftimizer.Simulator;
using Craftimizer.Simulator.Actions;
using Craftimizer.Solver;

namespace Craftimizer.Benchmark;

internal static class Program
{
    private static Task Main(string[] args)
    {
#if !IS_TRACE
        RunBench(args);
        return Task.CompletedTask;
#else
        return RunTrace();
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
                CLvl = 560,
            },
            new RecipeInfo()
            {
                IsExpert = false,
                ClassJobLevel = 90,
                RLvl = 640,
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
        //TypeLayout.PrintLayout<ArenaNode<SimulationNode>>(true);
        //return;

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
                CLvl = 560,
            },
            new RecipeInfo()
            {
                IsExpert = false,
                ClassJobLevel = 90,
                RLvl = 640,
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

        var sim = new SimulatorNoRandom(new(input));
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
