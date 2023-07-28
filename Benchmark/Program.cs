using Craftimizer.Simulator;
using Craftimizer.Simulator.Actions;
using Craftimizer.Solver;
using System.Diagnostics;

namespace Craftimizer.Benchmark;

internal static class Program
{
    private static void Main()
    {
        //var summary = BenchmarkRunner.Run<Bench>();
        //return;

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
        var (_, s) = Solver.Solver.SearchStepwise(config, state, a => Console.WriteLine(a));
        Console.WriteLine($"Qual: {s.Quality}/{s.Input.Recipe.MaxQuality}");
        return;

        Solver.Solver.SearchStepwiseFurcated(config, input);
        //Benchmark(() => );
    }

    private static void Benchmark(Func<SolverSolution> search)
    {
        var s = Stopwatch.StartNew();
        List<int> q = new();
        for (var i = 0; i < 15; ++i)
        {
            var state = search().State;
            //Console.WriteLine($"Qual: {state.Quality}/{state.Input.Recipe.MaxQuality}");

            q.Add(state.Quality);
        }

        s.Stop();
        Console.WriteLine($"{s.Elapsed.TotalMilliseconds / 60:0.00}ms/cycle");
        Console.WriteLine(string.Join(',', q));
        q.Sort();
        Console.WriteLine($"Min: {Quartile(q, 0)}, Max: {Quartile(q, 4)}, Avg: {Quartile(q, 2)}, Q1: {Quartile(q, 1)}, Q3: {Quartile(q, 3)}");
    }

    // https://stackoverflow.com/a/31536435
    private static float Quartile(List<int> input, int quartile)
    {
        float dblPercentage = quartile switch
        {
            0 => 0,     // Smallest value in the data set
            1 => 25,    // First quartile (25th percentile)
            2 => 50,    // Second quartile (50th percentile)
            3 => 75,    // Third quartile (75th percentile)
            4 => 100,   // Largest value in the data set
            _ => 0,
        };
        if (dblPercentage >= 100) return input[^1];

        var position = (input.Count + 1) * dblPercentage / 100f;
        var n = (dblPercentage / 100f * (input.Count - 1)) + 1;

        float leftNumber, rightNumber;
        if (position >= 1)
        {
            leftNumber = input[(int)MathF.Floor(n) - 1];
            rightNumber = input[(int)MathF.Floor(n)];
        }
        else
        {
            leftNumber = input[0]; // first data
            rightNumber = input[1]; // first data
        }

        if (leftNumber == rightNumber)
            return leftNumber;
        else
        {
            var part = n - MathF.Floor(n);
            return leftNumber + (part * (rightNumber - leftNumber));
        }
    }
}
